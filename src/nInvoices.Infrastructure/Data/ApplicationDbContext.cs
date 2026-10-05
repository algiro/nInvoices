using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using nInvoices.Core.Entities;
using nInvoices.Core.Interfaces;
using nInvoices.Infrastructure.Encryption;

namespace nInvoices.Infrastructure.Data;

/// <summary>
/// Main database context for the nInvoices application.
/// Handles entity mapping, relationships, and value object conversions.
/// Every <see cref="IOwnedEntity"/> is scoped to the current user: queries only see that user's
/// rows, and saves stamp and verify ownership.
/// Columns marked <see cref="EncryptedPropertyExtensions.IsEncrypted{TProperty}(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder{TProperty}, string)"/>
/// are encrypted with the current user's key (<see cref="FieldEncryptor"/>).
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public const int OwnerIdMaxLength = 255;

    private static readonly MethodInfo ConfigureOwnedEntityMethod = typeof(ApplicationDbContext)
        .GetMethod(nameof(ConfigureOwnedEntity), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly IUserContext? _userContext;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        FieldEncryptor encryptor,
        IUserContext? userContext = null)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(encryptor);
        Encryptor = encryptor;
        _userContext = userContext;
    }

    /// <summary>Encrypts the sensitive columns. Part of the model cache key: the converters use it.</summary>
    internal FieldEncryptor Encryptor { get; }

    /// <summary>
    /// Lets <see cref="LegacyDataEncryption"/> save Verifactu records again to encrypt them: their
    /// values don't change, only how they are stored.
    /// </summary>
    internal bool AllowRewritingVerifactuRecords { get; set; }

    /// <summary>
    /// The user whose data this context reads and writes. Null when there is no authenticated
    /// user, in which case owned queries return nothing and owned writes are refused.
    /// </summary>
    public string? CurrentOwnerId => _userContext?.UserId;

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<MonthlyReportTemplate> MonthlyReportTemplates => Set<MonthlyReportTemplate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceTaxLine> InvoiceTaxLines => Set<InvoiceTaxLine>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<WorkDayProject> WorkDayProjects => Set<WorkDayProject>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<InvoiceSequence> InvoiceSequences => Set<InvoiceSequence>();
    public DbSet<ImageAsset> ImageAssets => Set<ImageAsset>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<GmailConnection> GmailConnections => Set<GmailConnection>();
    public DbSet<OAuthState> OAuthStates => Set<OAuthState>();
    public DbSet<InvoiceEmail> InvoiceEmails => Set<InvoiceEmail>();
    public DbSet<HolidayCalendar> HolidayCalendars => Set<HolidayCalendar>();
    public DbSet<HolidayRule> HolidayRules => Set<HolidayRule>();
    public DbSet<ComplianceSettings> ComplianceSettings => Set<ComplianceSettings>();
    public DbSet<InvoiceEInvoice> InvoiceEInvoices => Set<InvoiceEInvoice>();
    public DbSet<EInvoiceSubmission> EInvoiceSubmissions => Set<EInvoiceSubmission>();
    public DbSet<VerifactuRecord> VerifactuRecords => Set<VerifactuRecord>();
    public DbSet<VerifactuSubmission> VerifactuSubmissions => Set<VerifactuSubmission>();
    public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, EncryptorModelCacheKeyFactory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        modelBuilder.ApplyEncryption(Encryptor);

        // "One active template per (customer, invoice type)" is a partial unique index.
        // The filter predicate is provider-specific: SQLite/SQL Server quote with [ ] and
        // compare bool as = 1; PostgreSQL quotes with " " and uses the bool column directly.
        // (SQLite keeps the exact existing string, so no migration churn.)
        modelBuilder.Entity<InvoiceTemplate>()
            .HasIndex(t => new { t.CustomerId, t.InvoiceType, t.IsActive })
            .IsUnique()
            .HasFilter(Database.IsNpgsql() ? "\"IsActive\"" : "[IsActive] = 1");

        // A template with no customer is shared by all of the user's customers, and the index
        // above treats every NULL customer as different: this one keeps a user's shared
        // templates to one active per invoice type
        modelBuilder.Entity<InvoiceTemplate>()
            .HasIndex(t => new { t.OwnerId, t.InvoiceType })
            .IsUnique()
            .HasDatabaseName("IX_InvoiceTemplates_Shared_OwnerId_InvoiceType")
            .HasFilter(Database.IsNpgsql() ? "\"IsActive\" AND \"CustomerId\" IS NULL" : "[IsActive] = 1 AND [CustomerId] IS NULL");

        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && !t.IsOwned() && typeof(IOwnedEntity).IsAssignableFrom(t.ClrType))
            .ToList())
        {
            ConfigureOwnedEntityMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ConfigureOwnedEntity<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IOwnedEntity
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.Property(e => e.OwnerId).HasMaxLength(OwnerIdMaxLength).IsRequired();
        entity.HasIndex(e => e.OwnerId);

        // CurrentOwnerId is re-evaluated per context instance, i.e. per request.
        entity.HasQueryFilter(e => e.OwnerId == CurrentOwnerId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Update timestamps
        var entries = ChangeTracker.Entries<EntityBase>();
        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureVerifactuRecordsAreAppendOnly();
        await ApplyOwnershipAsync(cancellationToken);

        var ownerId = CurrentOwnerId;
        if (string.IsNullOrEmpty(ownerId))
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        // Encrypted columns are written with the current user's key
        await Encryptor.EnsureKeyAsync(ownerId, cancellationToken);
        using (FieldEncryptor.BeginWriting(ownerId))
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Verifactu records form a hash chain that must not be altered: once saved, a record can be neither
    /// modified nor deleted, whoever asks (a deleted invoice, a stray update, a bug).
    /// </summary>
    private void EnsureVerifactuRecordsAreAppendOnly()
    {
        ChangeTracker.DetectChanges();

        if (ChangeTracker.Entries<VerifactuRecord>().Any(e =>
                e.State == EntityState.Deleted || (e.State == EntityState.Modified && !AllowRewritingVerifactuRecords)))
            throw new InvalidOperationException("Verifactu records are append-only: a saved record cannot be changed or deleted.");
    }

    /// <summary>
    /// Synchronous saves would skip the asynchronous ownership checks, so they are not allowed.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync.");

    /// <summary>
    /// Stamps new owned rows with the current user, and refuses writes that touch another
    /// user's rows or point a foreign key at a row the current user cannot see.
    /// </summary>
    private async Task ApplyOwnershipAsync(CancellationToken cancellationToken)
    {
        ChangeTracker.DetectChanges();

        var entries = ChangeTracker.Entries<IOwnedEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (entries.Count == 0)
            return;

        var ownerId = CurrentOwnerId;
        if (string.IsNullOrEmpty(ownerId))
            throw new InvalidOperationException("Cannot save user data without an authenticated user.");

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.OwnerId))
                entry.Entity.OwnerId = ownerId;

            var originalOwner = entry.State == EntityState.Added
                ? entry.Entity.OwnerId
                : entry.Property(e => e.OwnerId).OriginalValue;
            if (originalOwner != ownerId || entry.Entity.OwnerId != ownerId)
                throw new UnauthorizedAccessException(
                    $"{entry.Metadata.ClrType.Name} does not belong to the current user.");
        }

        foreach (var entry in entries.Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            await EnsureReferencesAreOwnedAsync(entry, ownerId, cancellationToken);
        }
    }

    private async Task EnsureReferencesAreOwnedAsync(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry,
        string ownerId,
        CancellationToken cancellationToken)
    {
        foreach (var foreignKey in entry.Metadata.GetForeignKeys())
        {
            var principalType = foreignKey.PrincipalEntityType;
            if (principalType.IsOwned() || !typeof(IOwnedEntity).IsAssignableFrom(principalType.ClrType))
                continue;

            var properties = foreignKey.Properties.Select(p => entry.Property(p.Name)).ToList();
            if (entry.State == EntityState.Modified && !properties.Any(p => p.IsModified))
                continue;

            // A temporary key means the principal is being inserted in this same save,
            // so it has just been stamped with the current owner.
            if (properties.Any(p => p.CurrentValue is null || p.IsTemporary))
                continue;

            var principal = await FindAsync(
                principalType.ClrType,
                properties.Select(p => p.CurrentValue).ToArray(),
                cancellationToken);
            if (principal is not IOwnedEntity owned || owned.OwnerId != ownerId)
                throw new UnauthorizedAccessException(
                    $"{entry.Metadata.ClrType.Name} references a {principalType.ClrType.Name} that does not belong to the current user.");
        }
    }
}

using Microsoft.EntityFrameworkCore;
using nInvoices.Core.Entities;

namespace nInvoices.Infrastructure.Data;

/// <summary>
/// Main database context for the nInvoices application.
/// Handles entity mapping, relationships, and value object conversions.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Rate> Rates => Set<Rate>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<MonthlyReportTemplate> MonthlyReportTemplates => Set<MonthlyReportTemplate>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceTaxLine> InvoiceTaxLines => Set<InvoiceTaxLine>();
    public DbSet<WorkDay> WorkDays => Set<WorkDay>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<InvoiceSequence> InvoiceSequences => Set<InvoiceSequence>();
    public DbSet<ImageAsset> ImageAssets => Set<ImageAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
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
}

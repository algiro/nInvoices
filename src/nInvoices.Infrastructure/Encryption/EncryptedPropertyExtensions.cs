using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace nInvoices.Infrastructure.Encryption;

/// <summary>
/// Marks columns as encrypted in the EF configurations; <see cref="ApplyEncryption"/> then gives
/// them their converters. An encrypted column is stored as text (or bytes) whatever its .NET type,
/// so it can't be filtered, sorted or summed in SQL: do that in memory.
/// </summary>
public static class EncryptedPropertyExtensions
{
    public const string PurposeAnnotation = "nInvoices:EncryptionPurpose";

    /// <param name="purpose">
    /// A fixed name for the column, authenticated with each value. Never change it once data is
    /// stored: values written under the old name would no longer decrypt.
    /// </param>
    public static PropertyBuilder<TProperty> IsEncrypted<TProperty>(this PropertyBuilder<TProperty> builder, string purpose)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        return builder.HasAnnotation(PurposeAnnotation, purpose);
    }

    /// <inheritdoc cref="IsEncrypted{TProperty}(PropertyBuilder{TProperty}, string)"/>
    public static ComplexTypePropertyBuilder<TProperty> IsEncrypted<TProperty>(this ComplexTypePropertyBuilder<TProperty> builder, string purpose)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        return builder.HasAnnotation(PurposeAnnotation, purpose);
    }

    public static string? GetEncryptionPurpose(this IReadOnlyProperty property) =>
        property.FindAnnotation(PurposeAnnotation)?.Value as string;

    /// <summary>Gives every property marked <see cref="IsEncrypted{TProperty}(PropertyBuilder{TProperty}, string)"/> its converter.</summary>
    public static void ApplyEncryption(this ModelBuilder modelBuilder, FieldEncryptor encryptor)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(encryptor);

        var purposes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            ApplyTo(entityType.GetDeclaredProperties(), encryptor, purposes);
            foreach (var complex in entityType.GetDeclaredComplexProperties())
                ApplyTo(complex.ComplexType, encryptor, purposes);
        }
    }

    private static void ApplyTo(IMutableComplexType complexType, FieldEncryptor encryptor, HashSet<string> purposes)
    {
        ApplyTo(complexType.GetDeclaredProperties(), encryptor, purposes);
        foreach (var nested in complexType.GetDeclaredComplexProperties())
            ApplyTo(nested.ComplexType, encryptor, purposes);
    }

    private static void ApplyTo(IEnumerable<IMutableProperty> properties, FieldEncryptor encryptor, HashSet<string> purposes)
    {
        foreach (var property in properties)
        {
            if (property.GetEncryptionPurpose() is not { } purpose)
                continue;
            if (!purposes.Add(purpose))
                throw new InvalidOperationException($"Encryption purpose '{purpose}' is used by more than one column.");

            property.SetValueConverter(ConverterFor(property, purpose, encryptor));
            if (property.ClrType == typeof(byte[]))
                property.SetValueComparer(ValueComparer.CreateDefault<byte[]>(favorStructuralComparisons: true));
            // The stored value is longer than the plain one and has no numeric precision
            property.SetMaxLength(null);
            property.SetPrecision(null);
            property.SetScale(null);
        }
    }

    private static ValueConverter ConverterFor(IMutableProperty property, string purpose, FieldEncryptor encryptor)
    {
        var type = property.ClrType;
        if (type == typeof(string))
            return new ValueConverter<string, string>(
                v => encryptor.EncryptText(purpose, v),
                v => encryptor.DecryptText(purpose, v));
        if (type == typeof(decimal))
            return new ValueConverter<decimal, string>(
                v => encryptor.EncryptText(purpose, v.ToString(CultureInfo.InvariantCulture)),
                v => decimal.Parse(encryptor.DecryptText(purpose, v), NumberStyles.Number, CultureInfo.InvariantCulture));
        if (type == typeof(byte[]))
            return new ValueConverter<byte[], byte[]>(
                v => encryptor.EncryptBytes(purpose, v),
                v => encryptor.DecryptBytes(purpose, v));

        throw new NotSupportedException(
            $"{property.DeclaringType.DisplayName()}.{property.Name}: encrypting {type.Name} columns is not supported.");
    }
}

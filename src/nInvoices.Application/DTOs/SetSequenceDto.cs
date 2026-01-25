namespace nInvoices.Application.DTOs;

/// <summary>
/// DTO for setting the global invoice sequence number.
/// </summary>
public sealed record SetSequenceDto
{
    /// <summary>
    /// The new sequence value. Must be at least 1.
    /// WARNING: Setting this too low can cause duplicate invoice numbers.
    /// </summary>
    public int Value { get; init; }
}

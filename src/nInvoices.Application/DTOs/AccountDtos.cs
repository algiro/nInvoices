namespace nInvoices.Application.DTOs;

/// <summary>Whether the account is already approved, and whether the administrator has been told it is waiting.</summary>
public sealed record AccessRequestResultDto(bool Approved, bool AdministratorNotified);

/// <summary>Account deletion must be confirmed by typing a word (see <c>DeleteAccountCommand.Confirmation</c>).</summary>
public sealed record DeleteAccountRequestDto(string? Confirmation);

public sealed record DeleteAccountResultDto(int DeletedRows);

namespace Trislab.Modules.Identity.Contracts.DTOs;

/// <summary>What a user picker shows of a user: no e-mail, phone or account status.</summary>
public sealed record UserLookupDto(string Id, string? UserName, string? FirstName, string? LastName, string? ImageUrl);

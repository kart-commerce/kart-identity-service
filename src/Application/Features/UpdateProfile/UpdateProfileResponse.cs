namespace Kart.Identity.Application.Features.UpdateProfile;

/// <summary>api-contract.yaml PATCH /auth/profile's 200 response schema.</summary>
public sealed record UpdateProfileResponse(string? Email, string DisplayName, DateTimeOffset UpdatedAt);

namespace Kart.Identity.Application.Features.EnrollMfa;

/// <summary>api-contract.yaml POST /auth/mfa/enroll 200 response.</summary>
public sealed record EnrollMfaResponse(string ProvisioningUri, DateTimeOffset SecretExpiresAt);

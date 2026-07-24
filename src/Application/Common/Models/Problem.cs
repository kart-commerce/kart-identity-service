namespace Kart.Identity.Application.Common.Models;

/// <summary>api-contract.yaml `components.schemas.Problem` — this platform's RFC 7807-style error shape.</summary>
public sealed record Problem(string Code, string Message, IReadOnlyDictionary<string, object?>? Details = null);

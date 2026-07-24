using MediatR;

namespace Kart.Identity.Application.Features.Login;

/// <summary>
/// api-contract.yaml POST /auth/login. <see cref="IpAddress"/> is not part of the
/// request body — it's the caller's remote address, resolved by the Api layer, for
/// per-IP progressive throttling (edge-cases.md, "Credential Stuffing / Brute-Force").
/// </summary>
public sealed record LoginCommand(string Email, string Password, string IpAddress) : IRequest<LoginResult>;

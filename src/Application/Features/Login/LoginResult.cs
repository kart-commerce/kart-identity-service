namespace Kart.Identity.Application.Features.Login;

/// <summary>
/// api-contract.yaml POST /auth/login's two success shapes (200 TokenPair vs. 202
/// MfaChallenge) — the Api layer pattern-matches on the concrete subtype to pick
/// the status code, since both are "no exception" outcomes from the handler's
/// point of view.
/// </summary>
public abstract record LoginResult;

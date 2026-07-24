namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml POST /auth/refresh 401 — covers an unknown token, an
/// expired-but-never-consumed one, and a genuine already-rotated-out replay
/// alike (edge-cases.md, "Refresh Token Replay After Rotation"). Only the
/// already-consumed-replay case actually revokes the session/family; the other
/// two are reported identically since the client's remedy is the same either
/// way — re-authenticate from /auth/login.
/// </summary>
public sealed class RefreshTokenReuseDetectedException() : Exception(
    "Refresh token reuse detected or token is invalid/expired. Re-authenticate from /auth/login.");

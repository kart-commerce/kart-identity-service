namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml POST /auth/refresh 409 — edge-cases.md, "Concurrent Refresh
/// Race": lost the DB-level conditional update to a concurrent request rotating
/// the same still-valid token. Expected, retriable outcome, not an infrastructure
/// error (design-decisions.md, "Concurrency Control for Refresh-Token Rotation").
/// </summary>
public sealed class RefreshTokenRaceLostException() : Exception(
    "Lost a concurrent refresh race for this token. Retry with the token returned by whichever request won, or retry the original request once.");

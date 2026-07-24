namespace Kart.Identity.Application.Common.Exceptions;

/// <summary>
/// api-contract.yaml POST /internal/users/{userId}/lock and .../unlock 404 —
/// "userId not found."
/// </summary>
public sealed class UserNotFoundException() : Exception("User not found.");

namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// event-contract.md `UserDataErased` payload (published by User Service, consumed here):
/// `userId`, `erasedAt`.
/// </summary>
public sealed record UserDataErasedEventPayload(Guid UserId, DateTimeOffset ErasedAt);

using MediatR;

namespace Kart.Identity.Application.Features.ConsumeUserDataErased;

/// <summary>
/// event-contract.md `UserDataErased` (consumed from User Service's
/// `user.exchange`, routing key `user.data-erased`, per this service's own
/// `identity.user-events.queue` — message-bus-manifest.json). <paramref name="ErasedAt"/>
/// is the event's own payload timestamp (when User Service performed the
/// erasure), carried through for audit/tracing only — the write timestamps this
/// handler stamps are Identity's own processing time, not this value.
/// </summary>
public sealed record ConsumeUserDataErasedCommand(Guid UserId, DateTimeOffset ErasedAt) : IRequest;

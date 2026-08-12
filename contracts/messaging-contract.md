# Kart Identity Service — Messaging Contract

Source of truth: [`contracts/message-bus-manifest.json`](../contracts/message-bus-manifest.json). Nothing in this doc is hand-maintained config — `RabbitMqTopologyProvisioner` (from `Kart.Shared.Messaging`) reads that manifest at startup and declares every exchange, queue, binding, DLQ, and retry tier from it. This doc is a human-readable index over that manifest plus the consumer-side manifests of the services that bind to it.

Last verified: 2026-07-30, against the current state of the repo (not the platform design docs — see the caveat at the bottom).

## Exchanges owned by this service

| Exchange | Type | Durable | Purpose |
|---|---|---|---|
| `identity.exchange` | topic | yes | All events published by Identity |
| `identity.dlx` | topic | yes | Dead-letter exchange for Identity's own consumed events |

## Published events

| Event | Exchange | Routing Key | Exchange Type |
|---|---|---|---|
| `UserRegistered` | `identity.exchange` | `identity.user.registered` | topic |
| `SessionCreated` | `identity.exchange` | `identity.session.created` | topic |
| `UserAccountUpdated` | `identity.exchange` | `identity.user-account.updated` | topic |

These three are the *only* events Identity ever publishes — enforced at the database level via a check constraint on `outbox_events.event_type` (every EF migration carries it), so nothing else can even be written to the outbox. MFA verification, SAML SSO, OIDC, and social login don't mint their own event types; they all resolve to one of the three above.

## Who consumes what

### `UserRegistered`

| Consumer | Queue | Retry Ladder | Dead-Letter Queue |
|---|---|---|---|
| **User Service** | `user.user-registered.queue` | 30s → 120s → 300s (3 tiers) | `user.user-registered.dlq` (on `user.dlx`) |

User Service dispatches `CreateUserProfileOnRegistrationCommand`, which creates the user's profile row — this is the trigger for profile creation.

### `SessionCreated`

**No live consumer today.** Nothing in the monorepo binds a queue to `identity.session.created`. The platform design docs (`kart-platform/docs/services/kart-analytics-service/event-contract.md`) describe Analytics as the intended consumer for login/session metrics, but `kart-analytics-service` is currently a README-only stub with no code — so this event is published into the void until that service exists.

### `UserAccountUpdated`

| Consumer | Queue | Retry Ladder | Dead-Letter Queue |
|---|---|---|---|
| **User Service** | `user.user-account-updated.queue` | 30s → 120s (2 tiers) | `user.user-account-updated.dlq` (on `user.dlx`) |

User Service dispatches `ReconcileIdentityContactCopyCommand` to keep its denormalized email/display-name copy in sync (last-write-wins on `updatedAt`; if this arrives before `UserRegistered`, it creates a shell profile rather than dropping the event).

### Summary

| Event | Real Consumer(s) | Documented-but-unbuilt Consumer(s) |
|---|---|---|
| `UserRegistered` | User Service | Notification, Analytics (stub repos, no code) |
| `SessionCreated` | — none — | Analytics (stub repo, no code) |
| `UserAccountUpdated` | User Service | Analytics (stub repo, no code) |

## Retry & dead-letter mechanics

Retry/DLQ behavior isn't a platform-wide fixed default — `Kart.Shared.Messaging` is manifest-driven. Each queue declares its own retry ladder (however many tiers, at whatever TTLs) in its owning service's manifest; different queues can and do carry different ladders.

**How a retry actually happens:**
1. On handler failure, the consumer stamps a per-service header (e.g. `x-user-service-retry-count`) and republishes to the next tier's retry queue via the default exchange.
2. Each retry queue is declared with `x-dead-letter-exchange: ""` and `x-dead-letter-routing-key: <mainQueue>`, so once its TTL expires, RabbitMQ redelivers the message to the main queue itself — no application-level polling.
3. Once retries are exhausted, the consumer `nack`s without requeue, and the message lands on the DLQ named in that queue's `deadLetter` block.

**Naming convention** (by convention, not enforced by code): DLQs are named `<consumer's own prefix>.<event-kebab-case>.dlq` and live on the *consumer's* side, never a shared/global DLQ. A publishing service's own outbox relay retries indefinitely against broker connectivity issues rather than dead-lettering — which is why Identity's manifest has no DLQ entries tied to the 3 events it publishes; any DLQ for them lives in the consumer's manifest (`user.dlx`, in this case).

## Identity's own consumed event (for completeness)

Identity is also a consumer, not just a publisher — for GDPR erasure:

| Consumed Event | From | Queue | Retry Ladder | Dead-Letter Queue |
|---|---|---|---|---|
| `UserDataErased` | User Service (`user.exchange` / `user.data-erased`) | `identity.user-events.queue` | 30s → 60s → 120s → 240s → 480s (5 tiers) | `identity.user-events.dlq` (on `identity.dlx`) |

This is the platform's "compliance-critical" retry tier — the deepest ladder in Identity's contract, with a `LogCritical` page-out on final exhaustion. `kart-cart-service` is the only other real (code-backed) consumer of `UserDataErased`; the other five services named in User Service's manifest comment (Order, Notification, Analytics, Review, Recommendation, Wishlist) are unimplemented stubs.

## Caveat

`kart-platform/docs/services/kart-identity-service/event-contract.md` and the analytics/notification design docs describe a richer consumer set (Notification, Analytics) and different DLQ names (publisher-prefixed, e.g. `identity.user-registered.dlq`) than what's actually running. Treat those docs as forward-looking design intent, not current state — this doc reflects what's in code and manifests today. Of the 19 services in the monorepo, only 10 have real implementations (kart-identity-service, kart-user-service, kart-cart-service, kart-category-service, kart-delivery-tracking-service, kart-inventory-service, kart-offer-service, kart-payment-service, kart-product-service, kart-search-service); the rest are README-only stubs.

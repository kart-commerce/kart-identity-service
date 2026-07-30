# kart-identity-service — Messaging Flow Diagram

Generated from [`message-bus-manifest.json`](./message-bus-manifest.json). Covers the full lifecycle:
publish fan-out, consume bindings, ack/nack branching, dead-letter routing, retry ladder escalation
(dashed TTL-expiry requeue back to the origin queue), and terminal DLQ parking.

Each exchange is labeled on two independent axes:
- **ownership**: owned by this service vs. external (bind-only, never declared here)
- **role**: publish source, dead-letter exchange (DLX), or bind-only

```mermaid
flowchart TD
    SVC["kart-identity-service"]

    %% ===== Exchanges =====
    EXIDENTITY{{"identity.exchange<br/>owned · publish-source"}}
    EXDLX{{"identity.dlx<br/>owned · dead-letter only"}}
    EXUSER[/"user.exchange<br/>external · bind-only"/]

    %% ===== 1. Publish path: fan-out per published event =====
    SVC --> EXIDENTITY
    EXIDENTITY -->|"routingKey: identity.user.registered"| EVT_UR(["UserRegistered"])
    EXIDENTITY -->|"routingKey: identity.session.created"| EVT_SC(["SessionCreated"])
    EXIDENTITY -->|"routingKey: identity.user-account.updated"| EVT_UAU(["UserAccountUpdated"])

    %% ===== 2. Consume path: bindings -> queues -> ack/nack =====
    EXUSER -->|"routingKey: user.data-erased"| QUSEREVENTS["identity.user-events.queue"]

    QUSEREVENTS -->|ack| OKUSEREVENTS(("processed"))
    QUSEREVENTS -->|nack| EXDLX

    %% ===== 3. Dead-letter + retry ladder (ascending TTL) =====
    %% -- identity.user-events.queue ladder --
    EXDLX -->|"deadLetter routingKey: identity.user-events.dlq"| RUSEREVENTS30["identity.user-events.retry.30s<br/>ttl: 30000ms"]
    RUSEREVENTS30 --> RUSEREVENTS60["identity.user-events.retry.60s<br/>ttl: 60000ms"]
    RUSEREVENTS60 --> RUSEREVENTS120["identity.user-events.retry.120s<br/>ttl: 120000ms"]
    RUSEREVENTS120 --> RUSEREVENTS240["identity.user-events.retry.240s<br/>ttl: 240000ms"]
    RUSEREVENTS240 --> RUSEREVENTS480["identity.user-events.retry.480s<br/>ttl: 480000ms"]
    RUSEREVENTS30 -.->|"TTL expiry: requeue"| QUSEREVENTS
    RUSEREVENTS60 -.->|"TTL expiry: requeue"| QUSEREVENTS
    RUSEREVENTS120 -.->|"TTL expiry: requeue"| QUSEREVENTS
    RUSEREVENTS240 -.->|"TTL expiry: requeue"| QUSEREVENTS
    RUSEREVENTS480 -.->|"TTL expiry: requeue"| QUSEREVENTS

    %% ===== 4. Final tier exhausted -> terminal DLQ =====
    RUSEREVENTS480 --> DLQUSEREVENTS[["identity.user-events.dlq"]]

    %% ===== Styling =====
    classDef ownedExchange fill:#2563eb,color:#fff,stroke:#1e3a8a,stroke-width:2px;
    classDef externalExchange fill:#ffffff,color:#374151,stroke:#6b7280,stroke-width:2px,stroke-dasharray: 5 5;
    classDef queue fill:#10b981,color:#fff,stroke:#065f46,stroke-width:2px;
    classDef retryTier fill:#f59e0b,color:#1f2937,stroke:#b45309,stroke-width:2px;
    classDef dlx fill:#7c3aed,color:#fff,stroke:#4c1d95,stroke-width:2px;
    classDef dlq fill:#dc2626,color:#fff,stroke:#7f1d1d,stroke-width:2px;

    class EXIDENTITY ownedExchange
    class EXDLX dlx
    class EXUSER externalExchange
    class QUSEREVENTS queue
    class RUSEREVENTS30,RUSEREVENTS60,RUSEREVENTS120,RUSEREVENTS240,RUSEREVENTS480 retryTier
    class DLQUSEREVENTS dlq
```

## Notes

- **`identity.exchange`** is owned and is a pure publish-source: all three domain events
  (`UserRegistered`, `SessionCreated`, `UserAccountUpdated`) are published to it, and no queue in
  this service binds back to it — identity does not self-consume its own events.
- **`identity.dlx`** is owned but is dead-letter-only — it never carries a published domain event,
  it only receives nacked messages from `identity.user-events.queue` and routes them into the
  5-tier retry ladder / terminal DLQ.
- **`user.exchange`** is external — owned by User Service, bind-only, never declared here.
  `identity.user-events.queue` binds to it on `user.data-erased` to drive GDPR erasure processing.
- The `identity.user-events.queue` retry ladder is the widest in the service: 5 tiers ascending
  30s → 60s → 120s → 240s → 480s, all requeuing back to `identity.user-events.queue` itself
  (per ADR-0016). After the 480s tier is exhausted the message parks in
  `identity.user-events.dlq`.
- Published domain events (`UserRegistered`, `SessionCreated`, `UserAccountUpdated`) have no DLQ of
  their own: the outbox relay retries indefinitely until RabbitMQ is reachable rather than
  dead-lettering.

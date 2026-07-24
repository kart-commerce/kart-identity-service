# Contracts

`api-contract.yaml` is a synced copy of the approved contract owned by
`kart-platform/docs/services/kart-identity-service/api-contract.yaml` (the
source of truth). It is vendored here so `tests/ContractTests` can validate
this service's actual HTTP responses against it in this repo's own CI,
without a cross-repo checkout. Update it only by re-copying the upstream
file after a new contract revision is approved there — never edit it
directly in this repo.

`message-bus-manifest.json` is likewise a synced copy of
`kart-platform/docs/services/kart-identity-service/message-bus-manifest.json`
— this service's own RabbitMQ topology (`identity.exchange`/`identity.dlx`,
owned by this service alone; no shared platform-wide exchange, per
`kart-conventions.md` §"RabbitMQ" and `kart-requirements.md` §8/§9). Vendored
here as the reference for whatever process eventually declares this
topology at startup (not yet implemented — same "not part of this vertical
slice" scope as the Outbox poller itself, `OutboxEvent.cs`). Update it only
by re-copying the upstream file after a manifest revision is approved there.

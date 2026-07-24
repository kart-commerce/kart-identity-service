# Contracts

`api-contract.yaml` is a synced copy of the approved contract owned by
`kart-platform/docs/services/kart-identity-service/api-contract.yaml` (the
source of truth). It is vendored here so `tests/ContractTests` can validate
this service's actual HTTP responses against it in this repo's own CI,
without a cross-repo checkout. Update it only by re-copying the upstream
file after a new contract revision is approved there — never edit it
directly in this repo.

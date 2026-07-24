# kart-identity-service

Platform AuthN, token issuance (JWT, RS256), session/refresh-token lifecycle,
TOTP-based MFA, RBAC role/claim issuance, and SSO/identity federation
(enterprise SAML/OIDC + customer social OIDC). Single issuer of platform role
claims and sole point of contact with any external IdP.

Design docs: `kart-platform/docs/services/kart-identity-service/`.

## Layout

Clean Architecture + Vertical Slice (`docs/standards/folder-structure.md` in
[agent-reusables](https://github.com/kakon-mehedi/agent-reusables)):

```
src/
├── Api/              # ASP.NET Core minimal API endpoints, thin
├── Application/       # Features/<UseCaseName>/ vertical slices (MediatR)
├── Domain/             # aggregates, entities, value objects, domain events
└── Infrastructure/    # EF Core/Redis/RabbitMQ implementations, outbox
tests/
├── UnitTests/          # colocated by feature, mirrors Application/Features
├── IntegrationTests/
└── ContractTests/      # validates live responses against contracts/api-contract.yaml
contracts/              # synced copy of the approved api-contract.yaml (see contracts/README.md)
```

## Running locally

Requires the .NET 8 SDK.

```
dotnet build
dotnet test
```

The RS256 signing key is a secret and is never committed. Set it via
environment variables before `dotnet run`:

```
export Jwt__SigningKey__Kid="<key-id>"
export Jwt__SigningKey__PrivateKeyPem="$(cat /path/to/dev-private-key.pem)"
dotnet run --project src/Api
```

Generate a throwaway local key with:

```
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out /path/to/dev-private-key.pem
```

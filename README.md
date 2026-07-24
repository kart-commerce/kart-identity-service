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
└── ContractTests/      # validates live responses against api-contract.yaml
```

## Running locally

Requires the .NET 8 SDK.

```
dotnet build
dotnet test
```

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

Requires the .NET 8 SDK, a PostgreSQL instance (with the `citext` and
`pgcrypto` extensions available — both are created automatically by the
migration), and a Redis instance (ephemeral security state — login-attempt
throttling, MFA challenges; design-decisions.md).

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

The database connection string is also a secret in production (set via
`ConnectionStrings__IdentityDb`); `src/Api/appsettings.Development.json`
already points `dotnet run`'s default (`Development`) environment at a local
instance matching:

```
docker run -d --name kart-identity-db -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=kart_identity -p 5432:5432 postgres:16
dotnet ef database update --project src/Infrastructure --startup-project src/Infrastructure
```

Redis's connection string (`ConnectionStrings__Redis`) is likewise
non-secret-in-dev but still supplied via env var / K8s Secret in production;
`appsettings.Development.json` points at a plain local instance:

```
docker run -d --name kart-identity-redis -p 6379:6379 redis:7
```

The AES-256 key used to encrypt TOTP secrets at rest (`mfa_credentials`,
requirement-spec.md §4) is also a secret in production (`Mfa__Encryption__KeyBase64`).
Generate a throwaway local one with:

```
openssl rand -base64 32
```

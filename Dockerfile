# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartIdentityService.sln nuget.config ./
COPY packages/ packages/
COPY src/Api/Kart.Identity.Api.csproj src/Api/
COPY src/Application/Kart.Identity.Application.csproj src/Application/
COPY src/Domain/Kart.Identity.Domain.csproj src/Domain/
COPY src/Infrastructure/Kart.Identity.Infrastructure.csproj src/Infrastructure/
COPY tests/UnitTests/Kart.Identity.UnitTests.csproj tests/UnitTests/
COPY tests/IntegrationTests/Kart.Identity.IntegrationTests.csproj tests/IntegrationTests/
COPY tests/ContractTests/Kart.Identity.ContractTests.csproj tests/ContractTests/
# The cache mount persists extracted NuGet packages under a stable id shared by every other
# kart-*-service Dockerfile, so restore stays fast (no re-download) even on a cache-miss here
# (e.g. after a .csproj change) as long as some other service's build already warmed it.
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet restore src/Api/Kart.Identity.Api.csproj

# Scoped to what dotnet publish actually needs -- src/ (source) and contracts/
# (message-bus-manifest.json is a <Content> item Kart.Identity.Api.csproj copies into the publish
# output). Previously `COPY . .` pulled in tests/, README.md, scripts/, etc. too, so editing any
# of those busted this layer -- and the publish below -- for no reason (they never reach the
# built image).
COPY src/ src/
COPY contracts/ contracts/
# --no-restore only skips re-resolving the dependency graph -- publish still reads the actual
# package DLLs from the global packages folder, so it needs the same cache mount as restore
# above (the mount isn't part of the image; without it here this folder is empty again).
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet publish src/Api/Kart.Identity.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.Identity.Api.dll"]

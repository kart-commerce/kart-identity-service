FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY KartIdentityService.sln .
COPY src/Api/Kart.Identity.Api.csproj src/Api/
COPY src/Application/Kart.Identity.Application.csproj src/Application/
COPY src/Domain/Kart.Identity.Domain.csproj src/Domain/
COPY src/Infrastructure/Kart.Identity.Infrastructure.csproj src/Infrastructure/
COPY tests/UnitTests/Kart.Identity.UnitTests.csproj tests/UnitTests/
COPY tests/IntegrationTests/Kart.Identity.IntegrationTests.csproj tests/IntegrationTests/
COPY tests/ContractTests/Kart.Identity.ContractTests.csproj tests/ContractTests/
RUN dotnet restore src/Api/Kart.Identity.Api.csproj

COPY . .
RUN dotnet publish src/Api/Kart.Identity.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.Identity.Api.dll"]

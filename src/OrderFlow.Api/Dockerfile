FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props dotnet-tools.json ./
COPY src/OrderFlow.Domain/OrderFlow.Domain.csproj src/OrderFlow.Domain/
COPY src/OrderFlow.Contracts/OrderFlow.Contracts.csproj src/OrderFlow.Contracts/
COPY src/OrderFlow.Application/OrderFlow.Application.csproj src/OrderFlow.Application/
COPY src/OrderFlow.Infrastructure/OrderFlow.Infrastructure.csproj src/OrderFlow.Infrastructure/
COPY src/OrderFlow.Api/OrderFlow.Api.csproj src/OrderFlow.Api/
RUN dotnet restore src/OrderFlow.Api/OrderFlow.Api.csproj && dotnet tool restore

COPY src/ src/
RUN dotnet publish src/OrderFlow.Api/OrderFlow.Api.csproj --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM build AS migrations-build
RUN ConnectionStrings__OrderFlow="Host=localhost" dotnet ef migrations bundle \
    --project src/OrderFlow.Infrastructure \
    --startup-project src/OrderFlow.Api \
    --configuration Release \
    --output /migrations/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrations
WORKDIR /migrations
COPY --from=migrations-build /migrations/efbundle .
USER $APP_UID
ENTRYPOINT ["./efbundle"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderFlow.Api.dll"]

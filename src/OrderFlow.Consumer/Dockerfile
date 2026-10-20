FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props dotnet-tools.json ./
COPY src/OrderFlow.Contracts/OrderFlow.Contracts.csproj src/OrderFlow.Contracts/
COPY src/OrderFlow.Consumer/OrderFlow.Consumer.csproj src/OrderFlow.Consumer/
RUN dotnet restore src/OrderFlow.Consumer/OrderFlow.Consumer.csproj && dotnet tool restore

COPY src/OrderFlow.Contracts/ src/OrderFlow.Contracts/
COPY src/OrderFlow.Consumer/ src/OrderFlow.Consumer/
RUN dotnet publish src/OrderFlow.Consumer/OrderFlow.Consumer.csproj --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM build AS migrations-build
RUN ConnectionStrings__ConsumerDb="Host=localhost" dotnet ef migrations bundle \
    --project src/OrderFlow.Consumer \
    --startup-project src/OrderFlow.Consumer \
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
ENTRYPOINT ["dotnet", "OrderFlow.Consumer.dll"]

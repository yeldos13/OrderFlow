FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrderFlow.WarehouseStub/OrderFlow.WarehouseStub.csproj src/OrderFlow.WarehouseStub/
RUN dotnet restore src/OrderFlow.WarehouseStub/OrderFlow.WarehouseStub.csproj

COPY src/OrderFlow.WarehouseStub/ src/OrderFlow.WarehouseStub/
RUN dotnet publish src/OrderFlow.WarehouseStub/OrderFlow.WarehouseStub.csproj --configuration Release --no-restore --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderFlow.WarehouseStub.dll"]

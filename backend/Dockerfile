# Run from the repository root: docker build -f backend/Dockerfile -t devflow-api:phase8 .
FROM mcr.microsoft.com/dotnet/sdk:10.0.403 AS build
WORKDIR /source
COPY global.json NuGet.Config ./
COPY backend/ backend/
RUN dotnet restore backend/src/DevFlow.Api/DevFlow.Api.csproj --locked-mode
RUN dotnet publish backend/src/DevFlow.Api/DevFlow.Api.csproj \
    --configuration Release --no-restore --output /out /p:UseAppHost=false
RUN dotnet restore backend/src/DevFlow.Migrator/DevFlow.Migrator.csproj --locked-mode
RUN dotnet publish backend/src/DevFlow.Migrator/DevFlow.Migrator.csproj \
    --configuration Release --no-restore --output /migration /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.14 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
COPY --from=build /out ./
COPY --from=build /migration /migration/
# Public RDS trust anchors; never an application credential.
ADD --checksum=sha256:90a6f1a7978a1b1b096dca6c57511ca48d3b8cf052f8e704baf358f34ea2f21b --chmod=644 https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem /app/certs/rds.pem
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DevFlow.Api.dll"]

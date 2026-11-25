FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/QOL.Domain/QOL.Domain.csproj backend/QOL.Domain/
COPY backend/QOL.Application/QOL.Application.csproj backend/QOL.Application/
COPY backend/QOL.Infrastructure/QOL.Infrastructure.csproj backend/QOL.Infrastructure/
COPY backend/QOL.API/QOL.API.csproj backend/QOL.API/
RUN dotnet restore backend/QOL.API/QOL.API.csproj
COPY backend/ backend/
WORKDIR /src/backend/QOL.API
RUN dotnet publish QOL.API.csproj --configuration Release --output /app/publish --no-restore
RUN dotnet tool install --tool-path /tools dotnet-ef --version 10.0.14
ENV PATH="/tools:${PATH}"
WORKDIR /src
RUN dotnet ef migrations bundle \
    --project backend/QOL.Infrastructure/QOL.Infrastructure.csproj \
    --startup-project backend/QOL.API/QOL.API.csproj \
    --output /app/migrate \
    --configuration Release

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/migrate ./migrate
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
ENTRYPOINT ["dotnet", "QOL.API.dll"]

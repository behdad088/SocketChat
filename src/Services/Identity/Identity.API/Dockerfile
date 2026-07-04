# ----------- Base Runtime Image -----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base

# Arguments for non-root user
ARG APP_UID=1654
ARG APP_USER=appuser

# Create non-root user/group only if not exists
RUN apt-get update && apt-get install -y --no-install-recommends passwd curl && \
    rm -rf /var/lib/apt/lists/* && \
    mkdir -p /app/keys && \
    getent group ${APP_UID} || groupadd -g ${APP_UID} appgroup && \
    id -u ${APP_UID} || useradd -u ${APP_UID} -g ${APP_UID} -M ${APP_USER} && \
    chown -R ${APP_UID}:${APP_UID} /app

WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# ----------- Build Stage -----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj and restore
COPY ["Services/Identity/Identity.API/Identity.API.csproj", "Services/Identity/Identity.API/"]
COPY ["Services/Shared/Shared.csproj", "Services/Shared/"]
COPY ["Directory.Packages.props", "/"]

RUN dotnet restore "Services/Identity/Identity.API/Identity.API.csproj"

# Copy the rest of the source code
COPY . .

# Build
WORKDIR "/src/Services/Identity/Identity.API"
RUN dotnet build "Identity.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ----------- Publish Stage -----------
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Identity.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ----------- Final Runtime Image -----------
FROM base AS final

# Copy published output
COPY --from=publish /app/publish .

RUN chown -R ${APP_UID}:${APP_UID} /app && chmod -R u+rwX /app

USER ${APP_UID}

ENTRYPOINT ["dotnet", "Identity.API.dll"]

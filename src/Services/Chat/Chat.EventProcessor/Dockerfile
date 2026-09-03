FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Services/Chat/Chat.EventProcessor/Chat.EventProcessor.csproj", "Services/Chat/Chat.EventProcessor/"]
COPY ["Services/Shared/Shared.csproj", "Services/Shared/"]
COPY ["Services/Chat/Chat/Chat.csproj", "Services/Chat/Chat/"]
COPY ["Directory.Packages.props", "/"]
RUN dotnet restore "Services/Chat/Chat.EventProcessor/Chat.EventProcessor.csproj"
COPY . .
WORKDIR "/src/Services/Chat/Chat.EventProcessor"
RUN dotnet build "./Chat.EventProcessor.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Chat.EventProcessor.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Chat.EventProcessor.dll"]

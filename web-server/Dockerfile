FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app/web-server
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["web-server/web-server.csproj", "web-server/"]
RUN dotnet restore "web-server/web-server.csproj"
COPY ["web-server/", "web-server/"]
COPY ["frontend/", "frontend/"]
WORKDIR /src/web-server
RUN dotnet publish "web-server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
COPY --from=build /app/publish .
COPY --from=build /src/frontend /app/frontend
ENTRYPOINT ["dotnet", "web-server.dll"]

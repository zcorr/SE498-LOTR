# --- Stage 1: Build ---
# Use the full .NET SDK image so we can compile the project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copy the project file first and restore dependencies
# (Docker caches this layer — if .csproj hasn't changed, it skips re-downloading packages)
COPY ["LotrApi/LotrApi.csproj", "LotrApi/"]
RUN dotnet restore "LotrApi/LotrApi.csproj"

# Now copy everything else and build
COPY . .
WORKDIR "/src/LotrApi"
RUN dotnet publish "LotrApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# --- Stage 2: Run ---
# Use the smaller runtime-only image (no compiler, smaller = safer + faster)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LotrApi.dll"]
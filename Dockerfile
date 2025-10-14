# --------------------------
# Build stage
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копіюємо все (як у твоєму старому варіанті)
COPY . .

# Заходимо в каталог проєкту
WORKDIR /app/WarOfMachinesAPI

# Відновлення й публікація
RUN dotnet restore WarOfMachinesAPI.csproj
RUN dotnet publish WarOfMachinesAPI.csproj -c Release -o /app/out /p:UseAppHost=false

# --------------------------
# Runtime stage
# --------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/out ./

# Render задає змінну PORT — слухаємо її
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
EXPOSE 10000

# УВАГА: назва DLL з логів publish — WarOfMachines.dll
ENTRYPOINT ["dotnet", "WarOfMachines.dll"]

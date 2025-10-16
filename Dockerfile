# --------------------------
# Build stage
# --------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Копіюємо все (як у твоєму старому варіанті)
COPY . .

# Заходимо в каталог проєкту
WORKDIR /app/KnightsApi

# Відновлення й публікація
RUN dotnet restore KnightsApi.csproj
RUN dotnet publish KnightsApi.csproj -c Release -o /app/out /p:UseAppHost=false

# --------------------------
# Runtime stage
# --------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/out ./

# Render задає змінну PORT — слухаємо її
ENV ASPNETCORE_URLS=http://0.0.0.0:${PORT}
EXPOSE 10000

# УВАГА: назва DLL з логів publish — KnightsApi.dll
ENTRYPOINT ["dotnet", "KnightsApi.dll"]

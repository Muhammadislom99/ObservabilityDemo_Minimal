FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Копируем только файл проекта (он находится в той же папке, что и Dockerfile)
COPY ["ObservabilityDemo_Minimal.csproj", "."]
RUN dotnet restore "ObservabilityDemo_Minimal.csproj"

# Копируем все остальные файлы из текущей директории
COPY . .
RUN dotnet build "ObservabilityDemo_Minimal.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ObservabilityDemo_Minimal.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ObservabilityDemo_Minimal.dll"]
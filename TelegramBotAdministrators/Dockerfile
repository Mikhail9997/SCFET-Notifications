FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TelegramBotAdministrators/TelegramBotAdministrators.csproj", "TelegramBotAdministrators/"]
RUN dotnet restore "TelegramBotAdministrators/TelegramBotAdministrators.csproj"
COPY . .
WORKDIR "/src/TelegramBotAdministrators"
RUN dotnet build "TelegramBotAdministrators.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TelegramBotAdministrators.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TelegramBotAdministrators.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["TelegramBotEmployee/TelegramBotEmployee.csproj", "TelegramBotEmployee/"]
COPY ["Application/Application.csproj", "Application/"]
COPY ["Core/Core.csproj", "Core/"]
COPY ["Infrastructure/Infrastructure.csproj", "Infrastructure/"]
RUN dotnet restore "TelegramBotEmployee/TelegramBotEmployee.csproj"

COPY ["TelegramBotEmployee/", "TelegramBotEmployee/"]
COPY ["Application/", "Application/"]
COPY ["Core/", "Core/"]
COPY ["Infrastructure/", "Infrastructure/"]

WORKDIR "/src/TelegramBotEmployee"
RUN dotnet build "TelegramBotEmployee.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "TelegramBotEmployee.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TelegramBotEmployee.dll"]

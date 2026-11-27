FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release

WORKDIR /src
COPY ["Apps/SurPlatform/Api/Api.csproj", "Apps/SurPlatform/Api/"]
COPY ["Apps/SurPlatform/Application/Application.csproj", "Apps/SurPlatform/Application/"]
COPY ["Apps/SurPlatform/Domain/Domain.csproj", "Apps/SurPlatform/Domain/"]
COPY ["Apps/SurPlatform/Infrastructure/Infrastructure.csproj", "Apps/SurPlatform/Infrastructure/"]

COPY ["Libs/Core/Core.Abstractions/Core.Abstractions.csproj", "Libs/Core/Core.Abstractions/"]
COPY ["Libs/Core/Core.Database/Core.Database.csproj", "Libs/Core/Core.Database/"]
COPY ["Libs/Core/Core.Api/Core.Api.csproj", "Libs/Core/Core.Api/"]

RUN dotnet restore "Apps/SurPlatform/Api/Api.csproj"
COPY . .
WORKDIR "/src/Apps/SurPlatform/Api"
RUN dotnet build "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]

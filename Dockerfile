# Phase 1 : Base d'exécution pour la production
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
# 🌟 IMPORTANT : On configure le port 10000 requis par Render
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

# Phase 2 : Restauration et build du projet
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copie des fichiers de projet et restauration des dépendances Nuget
COPY ["Finama.API/Finama.API.csproj", "Finama.API/"]
# 💡 Si ton API dépend d'autres projets locaux (ex: Finama.Core, Finama.Infrastructure),
# décommente et ajuste les lignes ci-dessous :
# COPY ["Finama.Core/Finama.Core.csproj", "Finama.Core/"]
# COPY ["Finama.Infrastructure/Finama.Infrastructure.csproj", "Finama.Infrastructure/"]

RUN dotnet restore "./Finama.API/Finama.API.csproj"

# Copie de tout le reste du code source
COPY . .
WORKDIR "/src/Finama.API"
RUN dotnet build "./Finama.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Phase 3 : Publication des binaires optimisés
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Finama.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Phase 4 : Image finale d'exécution
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Finama.API.dll"]
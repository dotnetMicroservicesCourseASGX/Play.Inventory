FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5004

ENV ASPNETCORE_URLS=http://+:5004

USER app
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
# WORKDIR /src
COPY ["src/Play.Inventory.Contracts/Play.Inventory.Contracts.csproj", "src/Play.Inventory.Contracts/"]
COPY ["src/Play.Inventory.Service/Play.Inventory.Service.csproj", "src/Play.Inventory.Service/"]

RUN --mount=type=secret,id=GH_OWNER,dst=/GH_OWNER --mount=type=secret,id=GH_PAT,dst=/GH_PAT \
    dotnet nuget add source --username USERNAME --password `cat /GH_PAT` --store-password-in-clear-text \
    --name github "https://nuget.pkg.github.com/`cat /GH_OWNER`/index.json"

RUN dotnet restore "src/Play.Inventory.Service/Play.Inventory.Service.csproj"
COPY ./src ./src
WORKDIR "/src/Play.Inventory.Service"
RUN dotnet build "Play.Inventory.Service.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "Play.Inventory.Service.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Play.Inventory.Service.dll"]

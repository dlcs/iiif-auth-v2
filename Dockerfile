FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["src/IIIFAuth2/IIIFAuth2.API/IIIFAuth2.API.csproj", "IIIFAuth2.API/"]
RUN dotnet restore "IIIFAuth2.API/IIIFAuth2.API.csproj"

COPY src/IIIFAuth2/ .
WORKDIR "/src/IIIFAuth2.API"
RUN dotnet build "IIIFAuth2.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IIIFAuth2.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim AS base

LABEL maintainer="Donald Gray <donald.gray@digirati.com>"
LABEL org.opencontainers.image.source=https://github.com/dlcs/iiif-auth-v2
LABEL org.opencontainers.image.description="IIIF Auth API v2 implementation for DLCS."

WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IIIFAuth2.API.dll"]
#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
RUN apt-get update && apt-get install -y libsodium-dev libopus-dev ffmpeg ca-certificates;
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Fixate.csproj", "."]
RUN dotnet restore "./Fixate.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Fixate.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Fixate.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Fixate.dll"]
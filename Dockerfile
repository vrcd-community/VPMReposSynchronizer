﻿FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VPMReposSynchronizer.Entry/VPMReposSynchronizer.Entry.csproj", "VPMReposSynchronizer.Entry/"]
RUN dotnet restore "VPMReposSynchronizer.Entry/VPMReposSynchronizer.Entry.csproj"
COPY ["VPMReposSynchronizer.Core/VPMReposSynchronizer.Core.csproj", "VPMReposSynchronizer.Core/"]
RUN dotnet restore "VPMReposSynchronizer.Core/VPMReposSynchronizer.Core.csproj"
COPY . .
WORKDIR "/src/VPMReposSynchronizer.Entry"
RUN dotnet build "VPMReposSynchronizer.Entry.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VPMReposSynchronizer.Entry.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VPMReposSynchronizer.Entry.dll"]

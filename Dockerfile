FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VPMReposSynchronizer.Entry/VPMReposSynchronizer.Entry.csproj", "VPMReposSynchronizer.Entry/"]
RUN dotnet restore "VPMReposSynchronizer.Entry/VPMReposSynchronizer.Entry.csproj"
COPY ["VPMReposSynchronizer.Core/VPMReposSynchronizer.Core.csproj", "VPMReposSynchronizer.Core/"]
RUN dotnet restore "VPMReposSynchronizer.Core/VPMReposSynchronizer.Core.csproj"
COPY ["FluentScheduler/FluentScheduler/FluentScheduler.csproj", "FluentScheduler/FluentScheduler/"]
RUN dotnet restore "FluentScheduler/FluentScheduler/FluentScheduler.csproj"

COPY ["FluentScheduler/.git", "FluentScheduler/"]
COPY . .

RUN apt-get update && apt-get install -y git

RUN dotnet build "VPMReposSynchronizer.Entry/VPMReposSynchronizer.Entry.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VPMReposSynchronizer.Entry/VPMReposSynchronizer.Entry.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "VPMReposSynchronizer.Entry.dll"]

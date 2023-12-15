FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["VPMReposSynchronizer.Web/VPMReposSynchronizer.Web.csproj", "VPMReposSynchronizer.Web/"]
RUN dotnet restore "VPMReposSynchronizer.Web/VPMReposSynchronizer.Web.csproj"
COPY . .
WORKDIR "/src/VPMReposSynchronizer.Web"
RUN dotnet build "VPMReposSynchronizer.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VPMReposSynchronizer.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VPMReposSynchronizer.Web.dll"]

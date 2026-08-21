# Development and CI only. The booth machine runs a self-contained win-x64
# publish installed as a Windows Service; it does not run containers.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props ChurchProjection.slnx ./
COPY src/ src/
RUN dotnet restore ChurchProjection.slnx
RUN dotnet publish src/ChurchProjection.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./

# Both are bind-mounted in compose. The SQLite file and the media library are
# the only state; everything else in this image is disposable.
ENV Storage__DatabasePath=/data/projection.db \
    Storage__MediaRoot=/data/media \
    ASPNETCORE_HTTP_PORTS=5000

VOLUME /data
EXPOSE 5000
ENTRYPOINT ["dotnet", "ChurchProjection.Api.dll"]

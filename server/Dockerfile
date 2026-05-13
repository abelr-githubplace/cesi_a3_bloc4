FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY EasySaveServer.csproj .
RUN dotnet restore
COPY src/ ./src/
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
VOLUME ["/app/logs"]
EXPOSE 9000
ENTRYPOINT ["dotnet", "EasySaveServer.dll"]

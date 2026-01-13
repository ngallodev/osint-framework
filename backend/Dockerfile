FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["OsintBackend/OsintBackend.csproj", "OsintBackend/"]
RUN dotnet restore "OsintBackend/OsintBackend.csproj"
COPY . .
WORKDIR "/src/OsintBackend"
RUN dotnet build "OsintBackend.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OsintBackend.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OsintBackend.dll"]

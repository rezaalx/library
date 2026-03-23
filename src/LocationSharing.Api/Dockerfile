FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/LocationSharing.Api/LocationSharing.Api.csproj", "src/LocationSharing.Api/"]
RUN dotnet restore "src/LocationSharing.Api/LocationSharing.Api.csproj"

COPY . .
WORKDIR "/src/src/LocationSharing.Api"
RUN dotnet publish "LocationSharing.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LocationSharing.Api.dll"]

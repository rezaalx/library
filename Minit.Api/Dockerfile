FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Minit.Api/Minit.Api.csproj", "Minit.Api/"]
RUN dotnet restore "Minit.Api/Minit.Api.csproj"

COPY . .
RUN dotnet publish "Minit.Api/Minit.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Minit.Api.dll"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["MyKicksBuddy.csproj", "./"]
RUN dotnet restore "MyKicksBuddy.csproj"

COPY . .
RUN dotnet publish "MyKicksBuddy.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyKicksBuddy.dll"]

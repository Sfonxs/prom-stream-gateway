# syntax=docker/dockerfile:1.5
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

COPY . .

# Restore the solution to fetch all dependencies
RUN dotnet restore prom-stream-gateway.sln

# Publish only your main project (remove the -o if you like default output)
RUN dotnet publish \
    ./PromStreamGateway.AspNetCore/PromStreamGateway.AspNetCore.csproj \
    -c Release \
    -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "PromStreamGateway.AspNetCore.dll"]

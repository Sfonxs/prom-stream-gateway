# syntax=docker/dockerfile:1.5
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Bring in source
COPY . .

# Restore and publish
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# Final image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "PromStreamGateway.AspNetCore.dll"]

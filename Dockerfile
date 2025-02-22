# syntax=docker/dockerfile:1.4

# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG TARGETARCH
WORKDIR /source

# Copy project file and restore as distinct layers
COPY --link PromStreamGateway.AspNetCore/*.csproj .
RUN dotnet restore -a $TARGETARCH

# Copy source code and publish app
COPY --link PromStreamGateway.AspNetCore/. .
RUN dotnet publish -a $TARGETARCH -o /app

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble
EXPOSE 8080
WORKDIR /app
COPY --link --from=build /app .
USER $APP_UID
ENTRYPOINT ["./PromStreamGateway.AspNetCore"]
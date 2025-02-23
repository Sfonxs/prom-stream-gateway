# syntax=docker/dockerfile:1.4

# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-noble AS build
ARG TARGETARCH
WORKDIR /source

RUN apt-get update && apt-get install -y clang zlib1g-dev gcc libc6-dev

# Copy project file and restore as distinct layers
COPY --link PromStreamGateway.AspNetCore/*.csproj .
RUN dotnet restore -a $TARGETARCH

# Copy source code and publish app
COPY --link PromStreamGateway.AspNetCore/. .
RUN dotnet publish -c Release -a $TARGETARCH -p:PublishAot=true --self-contained true -o /app

# Runtime stage
FROM debian:bookworm-slim AS runtime
WORKDIR /app
COPY --link --from=build /app .
RUN chmod +x PromStreamGateway.AspNetCore
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["/app/PromStreamGateway.AspNetCore"]
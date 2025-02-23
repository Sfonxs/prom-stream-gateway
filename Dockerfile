# syntax=docker/dockerfile:1.5

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# We'll figure out if we're building for ARM or x64
ARG TARGETPLATFORM
WORKDIR /source
COPY . .

# Install cross-binutils for ARM64 in case the build is running under x86 emulation:
RUN apt-get update && apt-get install -y clang binutils-aarch64-linux-gnu

RUN case "$TARGETPLATFORM" in \
      "linux/amd64")  export RID=linux-x64 ;; \
      "linux/arm64")  export RID=linux-arm64 ;; \
      *) echo "Unsupported platform: $TARGETPLATFORM" ; exit 1 ;; \
    esac \
 && dotnet restore \
      -r "$RID" \
      -c Release \
      # no shared cache mount here to avoid mixing x64/arm64 partial downloads
 && dotnet publish \
      -c Release \
      -r "$RID" \
      -p:PublishAot=true \
      --self-contained true \
      -o /app

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
WORKDIR /app
COPY --from=build /app ./
ENTRYPOINT ["./PromStreamGateway.AspNetCore"]

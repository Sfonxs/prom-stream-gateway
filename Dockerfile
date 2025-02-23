# syntax=docker/dockerfile:1.5

# === Stage 1: Build ===
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build

# Let us detect which architecture we're building on
ARG TARGETPLATFORM
WORKDIR /source

# Copy everything in; adjust if your repo layout differs
COPY . .

# Install cross-binutils if we detect an ARM build
# If building on ARM64 natively (through QEMU), you need arm64 binutils for AOT linking.
RUN apt-get update && apt-get install -y clang binutils-aarch64-linux-gnu

# Derive the .NET Runtime Identifier from TARGETPLATFORM
# For example, linux/amd64 -> linux-x64, linux/arm64 -> linux-arm64
RUN --mount=type=cache,target=/root/.nuget/packages \
    case "$TARGETPLATFORM" in \
      "linux/amd64")  export RID=linux-x64 ;; \
      "linux/arm64")  export RID=linux-arm64 ;; \
      *) echo "Unsupported platform: $TARGETPLATFORM" ; exit 1 ;; \
    esac \
 && dotnet publish -c Release \
    -r "$RID" \
    -p:PublishAot=true \
    --self-contained true \
    -o /app

# === Stage 2: Final Runtime Image ===
FROM mcr.microsoft.com/dotnet/runtime-deps:9.0
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app ./

ENTRYPOINT ["./PromStreamGateway.AspNetCore"]

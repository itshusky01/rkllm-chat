# syntax=docker/dockerfile:1.7

FROM ubuntu:22.04 AS build

ARG DEBIAN_FRONTEND=noninteractive
ARG NODE_MAJOR=22
ARG CONFIGURATION=Release
ARG RUNTIME=linux-arm64
ARG FRAMEWORK=net10.0

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    NUGET_XMLDOC_MODE=skip

SHELL ["/bin/bash", "-o", "pipefail", "-c"]

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        gnupg \
        wget \
        xz-utils \
        tar \
        clang \
        zlib1g-dev \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -o /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends dotnet-sdk-10.0 \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL "https://deb.nodesource.com/setup_${NODE_MAJOR}.x" | bash - \
    && apt-get update \
    && apt-get install -y --no-install-recommends nodejs \
    && node --version \
    && npm --version \
    && dotnet --version \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /workspace
COPY . .

RUN chmod +x scripts/build.sh \
    && CONFIGURATION="${CONFIGURATION}" RUNTIME="${RUNTIME}" FRAMEWORK="${FRAMEWORK}" ./scripts/build.sh

FROM scratch
COPY --from=build /workspace/dist/ /

FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0-bookworm

USER root

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        wget \
        unzip \
        jq \
        ca-certificates \
        procps \
        iputils-ping \
        postgresql-client \
        redis-tools \
    && rm -rf /var/lib/apt/lists/*

USER vscode

ENV PATH="/home/vscode/.dotnet/tools:${PATH}"

RUN dotnet tool install --global dotnet-ef

WORKDIR /workspaces/CebizPay
#!/usr/bin/env bash

set -e

echo "Setting up CebizPay development environment..."

# .NET
dotnet --info

# EF Core CLI
dotnet ef --version

# Docker
docker --version

# GitHub CLI
gh --version

# Node.js
node --version

# Antigravity CLI
if ! command -v antigravity >/dev/null 2>&1; then
    echo "Installing Antigravity CLI..."
    curl -fsSL https://antigravity.google/cli/install.sh | bash
else
    echo "Antigravity CLI already installed."
fi

echo ""
echo "Installed development tools:"
echo "-----------------------------"

dotnet --version
dotnet ef --version
docker --version
gh --version
node --version

if command -v antigravity >/dev/null 2>&1; then
    antigravity --version || true
fi

echo ""
echo "CebizPay development environment ready."
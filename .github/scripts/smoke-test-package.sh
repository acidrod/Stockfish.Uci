#!/usr/bin/env bash
# Restore from the produced local package only, not from NuGet.org or cache.
set -euo pipefail
package_directory=$(realpath "$1")
version=$2
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

dotnet new console --framework net10.0 --name PackageConsumer \
  --output "$work/consumer" --no-restore
dotnet add "$work/consumer/PackageConsumer.csproj" package Stockfish.Uci \
  --version "$version" --no-restore

cat > "$work/consumer/Program.cs" <<'CS'
using Stockfish.Uci;

var move = UciMove.Parse("e2e4");
if (move.Value != "e2e4")
{
    throw new InvalidOperationException("Unexpected result from the installed package.");
}

Console.WriteLine($"Package smoke test passed: {move}");
CS

export NUGET_PACKAGES="$work/packages"
dotnet restore "$work/consumer/PackageConsumer.csproj" --source "$package_directory"
dotnet run --project "$work/consumer/PackageConsumer.csproj" --configuration Release --no-restore

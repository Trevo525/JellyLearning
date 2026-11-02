#!/usr/bin/env bash
# Build the plugin and install it into the dev Jellyfin instance, then restart it.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
plugins="$root/dev/jellyfin/config/data/plugins"
dest="$plugins/E-Learning_0.0.0.0"

dotnet publish "$root/Jellyfin.Plugin.ELearning/Jellyfin.Plugin.ELearning.csproj" \
  -c Debug -o "$root/artifacts/dev" --nologo -v quiet

[ -d "$plugins" ] || { echo "No $plugins - start the dev server first (see dev/README.md)." >&2; exit 1; }
rm -rf "$dest" && mkdir -p "$dest"
cp "$root/artifacts/dev/Jellyfin.Plugin.ELearning."{dll,pdb} "$dest/"

docker compose -f "$root/dev/docker-compose.yml" restart jellyfin
echo "Deployed to $dest"

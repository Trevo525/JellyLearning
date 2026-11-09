#!/usr/bin/env bash
# Build the plugin and install it into the dev Jellyfin instance, then restart it. Uses the
# host's dotnet SDK when there is one (faster), otherwise the `plugin` service of dev/compose.yaml.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
compose=(docker compose -f "$root/dev/compose.yaml")
state="${DEV_STATE:-jellyfin}"; [[ $state = /* ]] || state="$root/dev/$state"
plugins="$state/config/data/plugins"
[ -d "$plugins" ] || { echo "No $plugins - start the dev server first: ${compose[*]} up -d" >&2; exit 1; }
if command -v dotnet >/dev/null; then
  dest="$plugins/JellyLearning_0.0.0.0"
  dotnet publish "$root/Jellyfin.Plugin.ELearning/Jellyfin.Plugin.ELearning.csproj" \
    -c Debug -o "$root/artifacts/dev" --nologo -v quiet
  # Remove every installed version (a release zip too): Jellyfin would load the highest one.
  rm -rf "$plugins"/JellyLearning_* && mkdir -p "$dest"
  cp "$root/artifacts/dev/Jellyfin.Plugin.ELearning."{dll,pdb} "$root/artifacts/dev/Markdig.dll" "$dest/"
  echo "Deployed to $dest"
else
  "${compose[@]}" run --rm plugin
fi
"${compose[@]}" restart jellyfin

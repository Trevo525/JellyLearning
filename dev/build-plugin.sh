#!/usr/bin/env bash
# Build the plugin and install it into the dev instance's plugins folder. Runs in the `plugin`
# service of dev/compose.yaml (dotnet SDK image, repo at /src read-only, dev state at /state),
# so a clone needs nothing but Docker. Builds a copy of the sources, so the repo gets no
# root-owned bin/obj folders; everything it writes in /state is handed to PUID:PGID.
set -euo pipefail
owner="${PUID:-1000}:${PGID:-1000}"
work=/tmp/src
mkdir -p "$work"
tar -C /src --exclude=./.git --exclude=./dev/jellyfin --exclude=./artifacts \
  --exclude='*/bin' --exclude='*/obj' -cf - . | tar -C "$work" -xf -
dotnet publish "$work/Jellyfin.Plugin.ELearning/Jellyfin.Plugin.ELearning.csproj" \
  -c Debug -o /tmp/out --nologo -v quiet

plugins=/state/config/data/plugins
dest="$plugins/JellyLearning_0.0.0.0"
mkdir -p "$plugins"
# Remove every installed version (a release zip too): Jellyfin would load the highest one.
rm -rf "$plugins"/JellyLearning_* && mkdir -p "$dest"
cp /tmp/out/Jellyfin.Plugin.ELearning.dll /tmp/out/Jellyfin.Plugin.ELearning.pdb /tmp/out/Markdig.dll "$dest/"
chown "$owner" /state /state/config /state/config/data "$plugins"
chown -R "$owner" "$dest" /state/.nuget
echo "Installed the plugin into $dest"

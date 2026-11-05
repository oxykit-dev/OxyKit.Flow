#!/bin/sh
set -eu
# Seed durable TemplatesRoot from image pack when the volume is empty.
SEED="${OXYKIT_TEMPLATES_SEED:-/app/templates}"
ROOT="${Rendering__TemplatesRoot:-/data/templates}"
mkdir -p "$ROOT"
if [ -d "$SEED" ] && [ -z "$(ls -A "$ROOT" 2>/dev/null || true)" ]; then
  cp -a "$SEED"/. "$ROOT"/
fi
exec dotnet OxyKit.Render.Api.dll

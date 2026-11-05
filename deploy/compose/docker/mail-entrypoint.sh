#!/bin/sh
set -eu
SEED="${OXYKIT_MAIL_TEMPLATES_SEED:-/app/templates}"
ROOT="${MailTemplates__TemplatesRoot:-/data/templates}"
mkdir -p "$ROOT"
if [ -d "$SEED" ] && [ -z "$(ls -A "$ROOT" 2>/dev/null || true)" ]; then
  cp -a "$SEED"/. "$ROOT"/
fi
exec dotnet OxyKit.Mail.dll

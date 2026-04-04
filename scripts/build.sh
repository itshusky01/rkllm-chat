#!/bin/sh
set -eu

exec dotnet publish -c Release -r linux-arm64 /p:PublishAot=true /p:StripSymbols=false "$@"

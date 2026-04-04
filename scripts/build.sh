#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
CONFIGURATION=${CONFIGURATION:-Release}
RUNTIME=${RUNTIME:-linux-arm64}
FRAMEWORK=${FRAMEWORK:-net10.0}
PUBLISH_DIR="$ROOT_DIR/bin/$CONFIGURATION/$FRAMEWORK/$RUNTIME/publish"
UI_DIR="$ROOT_DIR/ui"
UI_DIST_DIR="$UI_DIR/dist/rkllm_ui/browser"
DIST_DIR="$ROOT_DIR/dist"
PACKAGE_NAME="rkllm-$RUNTIME"
PACKAGE_DIR="$DIST_DIR/$PACKAGE_NAME"
ARCHIVE_PATH="$DIST_DIR/$PACKAGE_NAME.tar.gz"

printf '\n==> Building Angular UI\n'
cd "$UI_DIR"
if [ ! -d node_modules ]; then
    if [ -f package-lock.json ]; then
        npm ci
    else
        npm install
    fi
fi
npm run build

if [ ! -d "$UI_DIST_DIR" ]; then
    UI_DIST_DIR="$UI_DIR/dist/rkllm_ui"
fi

if [ ! -d "$UI_DIST_DIR" ]; then
    echo "UI build output not found: $UI_DIST_DIR" >&2
    exit 1
fi

printf '\n==> Publishing C# server\n'
cd "$ROOT_DIR"
dotnet publish -c "$CONFIGURATION" -r "$RUNTIME" /p:PublishAot=true /p:StripSymbols=false "$@"

printf '\n==> Copying UI files into publish/wwwroot\n'
rm -rf "$PUBLISH_DIR/wwwroot"
mkdir -p "$PUBLISH_DIR/wwwroot"
cp -a "$UI_DIST_DIR"/. "$PUBLISH_DIR/wwwroot/"

printf '\n==> Creating tar.gz package\n'
rm -rf "$PACKAGE_DIR"
mkdir -p "$PACKAGE_DIR"
cp -a "$PUBLISH_DIR"/. "$PACKAGE_DIR/"
mkdir -p "$DIST_DIR"
tar -C "$DIST_DIR" -czf "$ARCHIVE_PATH" "$PACKAGE_NAME"

printf '\nPackage created: %s\n' "$ARCHIVE_PATH"


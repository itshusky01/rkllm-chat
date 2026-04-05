#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
CONFIGURATION=${CONFIGURATION:-Release}
RUNTIME=${RUNTIME:-linux-arm64}
FRAMEWORK=${FRAMEWORK:-net10.0}
PROJECT_FILE=${PROJECT_FILE:-RkllmChat.csproj}
PUBLISH_DIR="$ROOT_DIR/bin/$CONFIGURATION/$FRAMEWORK/$RUNTIME/publish"
UI_DIR="$ROOT_DIR/ui"
DIST_DIR="$ROOT_DIR/dist"
PACKAGE_NAME="rkllm-$RUNTIME"
PACKAGE_DIR="$DIST_DIR/$PACKAGE_NAME"
ARCHIVE_PATH="$DIST_DIR/$PACKAGE_NAME.tar.gz"
MODE=${1:-all}

print_usage() {
    cat <<EOF
Usage:
  ./scripts/build.sh                # build frontend + backend + tar.gz + .deb
  ./scripts/build.sh frontend       # build Angular UI only
  ./scripts/build.sh backend        # build .NET backend only
  ./scripts/build.sh deb            # build frontend + backend + .deb package
  ./scripts/build.sh all            # same as default

You can still pass extra dotnet publish args after the mode, for example:
  ./scripts/build.sh backend --self-contained false
EOF
}

case "$MODE" in
    frontend|front|ui)
        [ "$#" -gt 0 ] && shift
        MODE=frontend
        ;;
    backend|back|server|api)
        [ "$#" -gt 0 ] && shift
        MODE=backend
        ;;
    deb|package|pkg)
        [ "$#" -gt 0 ] && shift
        MODE=deb
        ;;
    all)
        [ "$#" -gt 0 ] && shift
        MODE=all
        ;;
    -h|--help|help)
        print_usage
        exit 0
        ;;
    *)
        MODE=all
        ;;
esac

resolve_ui_dist_dir() {
    if [ -d "$UI_DIR/dist/rkllm_ui/browser" ]; then
        printf '%s' "$UI_DIR/dist/rkllm_ui/browser"
        return 0
    fi

    if [ -d "$UI_DIR/dist/rkllm_ui" ]; then
        printf '%s' "$UI_DIR/dist/rkllm_ui"
        return 0
    fi

    return 1
}

build_frontend() {
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

    UI_DIST_DIR=$(resolve_ui_dist_dir || true)
    if [ -z "$UI_DIST_DIR" ] || [ ! -d "$UI_DIST_DIR" ]; then
        echo "UI build output not found under $UI_DIR/dist" >&2
        exit 1
    fi

    printf 'UI output: %s\n' "$UI_DIST_DIR"
}

build_backend() {
    printf '\n==> Publishing C# server\n'
    cd "$ROOT_DIR"
    dotnet publish "$PROJECT_FILE" -c "$CONFIGURATION" -r "$RUNTIME" /p:PublishAot=true /p:StripSymbols=false "$@"
}

prepare_wwwroot() {
    UI_DIST_DIR=$(resolve_ui_dist_dir || true)
    if [ -z "$UI_DIST_DIR" ] || [ ! -d "$UI_DIST_DIR" ]; then
        echo "UI build output not found under $UI_DIR/dist" >&2
        exit 1
    fi

    printf '\n==> Copying UI files into publish/wwwroot\n'
    rm -rf "$PUBLISH_DIR/wwwroot"
    mkdir -p "$PUBLISH_DIR/wwwroot"
    cp -a "$UI_DIST_DIR"/. "$PUBLISH_DIR/wwwroot/"
}

package_tarball() {
    prepare_wwwroot

    printf '\n==> Creating tar.gz package\n'
    rm -rf "$PACKAGE_DIR"
    mkdir -p "$PACKAGE_DIR"
    cp -a "$PUBLISH_DIR"/. "$PACKAGE_DIR/"
    mkdir -p "$DIST_DIR"
    tar -C "$DIST_DIR" -czf "$ARCHIVE_PATH" "$PACKAGE_NAME"

    printf '\nPackage created: %s\n' "$ARCHIVE_PATH"
}

package_deb() {
    prepare_wwwroot
    chmod +x "$ROOT_DIR/scripts/package-deb.sh" "$ROOT_DIR/scripts/run-service.sh"
    CONFIGURATION="$CONFIGURATION" RUNTIME="$RUNTIME" FRAMEWORK="$FRAMEWORK" PROJECT_FILE="$PROJECT_FILE" \
        "$ROOT_DIR/scripts/package-deb.sh"
}

case "$MODE" in
    frontend)
        build_frontend
        ;;
    backend)
        build_backend "$@"
        ;;
    deb)
        build_frontend
        build_backend "$@"
        package_deb
        ;;
    all)
        build_frontend
        build_backend "$@"
        package_tarball
        package_deb
        ;;
esac


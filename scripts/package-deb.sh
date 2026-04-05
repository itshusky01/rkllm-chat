#!/bin/sh
set -eu

ROOT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
CONFIGURATION=${CONFIGURATION:-Release}
RUNTIME=${RUNTIME:-linux-arm64}
FRAMEWORK=${FRAMEWORK:-net10.0}
PROJECT_FILE=${PROJECT_FILE:-RkllmChat.csproj}
APP_NAME=${APP_NAME:-rkllm-chat}
BIN_NAME=${BIN_NAME:-rkllm-chat-server}
DIST_DIR="$ROOT_DIR/dist"
PUBLISH_DIR="$ROOT_DIR/bin/$CONFIGURATION/$FRAMEWORK/$RUNTIME/publish"
UI_DIR="$ROOT_DIR/ui"
SERVICE_NAME="$APP_NAME.service"
PACKAGE_VERSION=${PACKAGE_VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT_DIR/$PROJECT_FILE" | head -n 1)}
PACKAGE_VERSION=${PACKAGE_VERSION:-0.1.0}
MAINTAINER=${MAINTAINER:-RKLLM Chat Maintainers <noreply@example.com>}
DESCRIPTION=${DESCRIPTION:-RKLLM Chat local inference service with OpenAI/Ollama compatible APIs}

map_deb_arch() {
    case "$1" in
        linux-arm64) printf '%s' 'arm64' ;;
        linux-x64) printf '%s' 'amd64' ;;
        linux-arm) printf '%s' 'armhf' ;;
        *) printf '%s' 'all' ;;
    esac
}

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

ensure_publish_exists() {
    if [ -d "$PUBLISH_DIR" ] && [ -f "$PUBLISH_DIR/$BIN_NAME" ]; then
        return 0
    fi

    printf '\n==> Publish directory not found, running dotnet publish\n'
    cd "$ROOT_DIR"
    dotnet publish "$PROJECT_FILE" -c "$CONFIGURATION" -r "$RUNTIME" /p:PublishAot=true /p:StripSymbols=false
}

prepare_wwwroot() {
    UI_DIST_DIR=$(resolve_ui_dist_dir || true)
    if [ -z "$UI_DIST_DIR" ] || [ ! -d "$UI_DIST_DIR" ]; then
        printf 'UI build output not found, keeping existing wwwroot if present.\n'
        return 0
    fi

    printf '\n==> Copying UI files into publish/wwwroot\n'
    rm -rf "$PUBLISH_DIR/wwwroot"
    mkdir -p "$PUBLISH_DIR/wwwroot"
    cp -a "$UI_DIST_DIR"/. "$PUBLISH_DIR/wwwroot/"
}

write_control_files() {
    debian_dir=$1

    cat > "$debian_dir/control" <<EOF
Package: $APP_NAME
Version: $PACKAGE_VERSION
Section: utils
Priority: optional
Architecture: $(map_deb_arch "$RUNTIME")
Maintainer: $MAINTAINER
Depends: systemd
Description: $DESCRIPTION
EOF

    cat > "$debian_dir/postinst" <<'EOF'
#!/bin/sh
set -e

if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload || true
    systemctl enable rkllm-chat.service >/dev/null 2>&1 || true

    if systemctl is-active --quiet rkllm-chat.service; then
        systemctl restart rkllm-chat.service || true
    fi
fi
EOF

    cat > "$debian_dir/prerm" <<'EOF'
#!/bin/sh
set -e

if command -v systemctl >/dev/null 2>&1; then
    systemctl stop rkllm-chat.service >/dev/null 2>&1 || true
    systemctl disable rkllm-chat.service >/dev/null 2>&1 || true
    systemctl daemon-reload || true
fi
EOF

    cat > "$debian_dir/postrm" <<'EOF'
#!/bin/sh
set -e

if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload || true
fi
EOF

    chmod 0755 "$debian_dir/postinst" "$debian_dir/prerm" "$debian_dir/postrm"
}

main() {
    if ! command -v dpkg-deb >/dev/null 2>&1; then
        echo 'dpkg-deb is required to build the .deb package.' >&2
        exit 1
    fi

    ensure_publish_exists
    prepare_wwwroot

    DEB_ARCH=$(map_deb_arch "$RUNTIME")
    PACKAGE_BASENAME="${APP_NAME}_${PACKAGE_VERSION}_${DEB_ARCH}"
    STAGE_DIR="$DIST_DIR/$PACKAGE_BASENAME"
    APP_DIR="$STAGE_DIR/opt/$APP_NAME"
    BIN_DIR="$STAGE_DIR/usr/bin"
    SYSTEMD_DIR="$STAGE_DIR/lib/systemd/system"
    DEFAULT_DIR="$STAGE_DIR/etc/default"
    DEBIAN_DIR="$STAGE_DIR/DEBIAN"
    PACKAGE_PATH="$DIST_DIR/${PACKAGE_BASENAME}.deb"

    printf '\n==> Preparing Debian package layout\n'
    rm -rf "$STAGE_DIR"
    mkdir -p "$APP_DIR" "$BIN_DIR" "$SYSTEMD_DIR" "$DEFAULT_DIR" "$DEBIAN_DIR" "$DIST_DIR"

    cp -a "$PUBLISH_DIR"/. "$APP_DIR/"
    find "$APP_DIR" -type f \( -name '*.so' -o -name '*.so.*' \) -delete

    install -m 0755 "$ROOT_DIR/scripts/run-service.sh" "$APP_DIR/run-service.sh"
    install -m 0644 "$ROOT_DIR/packaging/systemd/rkllm-chat.service" "$SYSTEMD_DIR/$SERVICE_NAME"
    install -m 0644 "$ROOT_DIR/packaging/systemd/rkllm-chat.env" "$DEFAULT_DIR/$APP_NAME"
    ln -sf "/opt/$APP_NAME/run-service.sh" "$BIN_DIR/$APP_NAME"

    write_control_files "$DEBIAN_DIR"

    printf '\n==> Building .deb package\n'
    dpkg-deb --build --root-owner-group "$STAGE_DIR" "$PACKAGE_PATH"

    printf '\nDebian package created: %s\n' "$PACKAGE_PATH"
}

main "$@"

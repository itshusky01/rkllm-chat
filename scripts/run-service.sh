#!/bin/sh
set -eu

APP_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
export LD_LIBRARY_PATH="$APP_DIR:$APP_DIR/libs${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

if [ -n "${RKLLM_CHAT_ARGS:-}" ]; then
    # shellcheck disable=SC2086
    exec "$APP_DIR/rkllm-chat-server" ${RKLLM_CHAT_ARGS}
fi

exec "$APP_DIR/rkllm-chat-server"

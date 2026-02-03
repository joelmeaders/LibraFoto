#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

source "$THIS_DIR/test_helpers.sh"

START_KIOSK_SCRIPT="$ROOT_DIR/scripts/start-kiosk.sh"

MOCK_BIN=""
TEMP_HOME=""
CHROMIUM_LOG=""

setUp() {
    MOCK_BIN="$(make_temp_dir)"
    TEMP_HOME="$(make_temp_dir)"
    CHROMIUM_LOG="$(make_temp_dir)/chromium.log"

    create_mock_command "$MOCK_BIN" "chromium" "echo \"chromium called\" >> \"$CHROMIUM_LOG\"; exit 0"
    create_mock_command "$MOCK_BIN" "curl" "echo 200"
    create_mock_command "$MOCK_BIN" "xset" "exit 0"
    create_mock_command "$MOCK_BIN" "unclutter" "exit 0"
    create_mock_command "$MOCK_BIN" "sleep" "exit 0"

    export HOME="$TEMP_HOME"
    export PATH="$MOCK_BIN:$PATH"
}

test_start_kiosk_invokes_chromium() {
    bash "$START_KIOSK_SCRIPT" >/dev/null 2>&1
    assertFileContains "$CHROMIUM_LOG" "chromium called"
}

SHUNIT2_PATH=$(find_shunit2) || {
    echo "shUnit2 not found. Set SHUNIT2_PATH or install shunit2." >&2
    exit 1
}

. "$SHUNIT2_PATH"

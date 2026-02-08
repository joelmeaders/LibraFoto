#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

source "$THIS_DIR/test_helpers.sh"

MOCK_BIN=""
TEMP_ROOT=""
SCRIPT_PATH=""
ENV_FILE=""

setUp() {
    MOCK_BIN="$(make_temp_dir)"
    TEMP_ROOT="$(make_temp_dir)"

    mkdir -p "$TEMP_ROOT/scripts"
    mkdir -p "$TEMP_ROOT/docker"

    SCRIPT_PATH="$TEMP_ROOT/scripts/update-host-ip.sh"
    cp "$ROOT_DIR/scripts/update-host-ip.sh" "$SCRIPT_PATH"

    ENV_FILE="$TEMP_ROOT/docker/.env"

    create_mock_command "$MOCK_BIN" "hostname" "if [[ \"$1\" == \"-I\" ]]; then echo 192.168.1.10; else exit 1; fi"
    create_mock_command "$MOCK_BIN" "ip" "echo '1.1.1.1 via 1.1.1.1 dev eth0 src 192.168.1.10'"
    create_mock_command "$MOCK_BIN" "docker" "exit 1"

    export PATH="$MOCK_BIN:$PATH"
}

test_update_host_ip_updates_env_value() {
    echo "LIBRAFOTO_HOST_IP=1.1.1.1" > "$ENV_FILE"

    bash "$SCRIPT_PATH" >/dev/null 2>&1
    local status=$?

    assertEquals 0 $status
    assertFileContains "$ENV_FILE" "LIBRAFOTO_HOST_IP=192.168.1.10"
}

test_update_host_ip_fails_when_env_missing() {
    rm -f "$ENV_FILE"

    bash "$SCRIPT_PATH" >/dev/null 2>&1
    local status=$?

    assertEquals 1 $status
}

SHUNIT2_PATH=$(find_shunit2) || {
    echo "shUnit2 not found. Set SHUNIT2_PATH or install shunit2." >&2
    exit 1
}

. "$SHUNIT2_PATH"

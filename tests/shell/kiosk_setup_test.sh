#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

source "$THIS_DIR/test_helpers.sh"

COMMON_SCRIPT="$ROOT_DIR/scripts/common.sh"
KIOSK_SCRIPT="$ROOT_DIR/scripts/kiosk-setup.sh"

TEST_LOG_FILE=""
TEMP_HOME=""
MOCK_BIN=""
APT_LOG=""

oneTimeSetUp() {
    TEST_LOG_FILE="$(make_temp_dir)/kiosk.log"
    TEMP_HOME="$(make_temp_dir)"
    MOCK_BIN="$(make_temp_dir)"
    APT_LOG="$(make_temp_dir)/apt.log"

    export LOG_FILE="$TEST_LOG_FILE"
    export HOME="$TEMP_HOME"
    export KIOSK_SCRIPT_SOURCED=true

    source "$COMMON_SCRIPT"
    source "$KIOSK_SCRIPT"

    create_mock_command "$MOCK_BIN" "apt-get" "echo \"apt-get \$*\" >> \"$APT_LOG\"; exit 0"
    create_mock_command "$MOCK_BIN" "apt-cache" "exit 1"
    create_mock_command "$MOCK_BIN" "dpkg" "exit 1"

    PATH="$MOCK_BIN:$PATH"
}

test_kiosk_install_dependencies_prefers_chromium_when_browser_pkg_missing() {
    kiosk_install_dependencies
    assertFileContains "$APT_LOG" "install -y chromium"
}

test_kiosk_create_startup_script_creates_script_in_home() {
    get_pi_home() {
        echo "$TEMP_HOME"
    }

    get_pi_user() {
        echo "pi"
    }

    kiosk_create_startup_script

    assertFileExists "$TEMP_HOME/start-kiosk.sh"
    [[ -x "$TEMP_HOME/start-kiosk.sh" ]]
    assertEquals 0 $?
}

SHUNIT2_PATH=$(find_shunit2) || {
    echo "shUnit2 not found. Set SHUNIT2_PATH or install shunit2." >&2
    exit 1
}

. "$SHUNIT2_PATH"

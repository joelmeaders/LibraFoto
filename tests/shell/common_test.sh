#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

source "$THIS_DIR/test_helpers.sh"

COMMON_SCRIPT="$ROOT_DIR/scripts/common.sh"

TEST_LOG_FILE=""

oneTimeSetUp() {
    TEST_LOG_FILE="$(make_temp_dir)/common.log"
    export LOG_FILE="$TEST_LOG_FILE"
    source "$COMMON_SCRIPT"
}

test_log_init_creates_header() {
    log_init "Test Script"
    assertFileExists "$TEST_LOG_FILE"
    assertFileContains "$TEST_LOG_FILE" "Test Script Log"
}

test_log_info_writes_log() {
    log_init "Test Script"
    log_info "hello world"
    assertFileContains "$TEST_LOG_FILE" "[INFO]"
    assertFileContains "$TEST_LOG_FILE" "hello world"
}

test_confirm_prompt_defaults_yes_on_empty_input() {
    confirm_prompt "Continue?" "Y" <<< ""
    assertEquals 0 $?
}

test_check_command_detects_existing_binary() {
    check_command bash
    assertEquals 0 $?
}

test_get_pi_user_defaults_to_pi() {
    unset SUDO_USER || true
    local user
    user=$(get_pi_user)
    assertEquals "pi" "$user"
}

test_get_current_version_reads_file() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    echo "2.5.1" > "$temp_dir/.version"
    local version
    version=$(get_current_version "$temp_dir")
    assertEquals "2.5.1" "$version"
}

SHUNIT2_PATH=$(find_shunit2) || {
    echo "shUnit2 not found. Set SHUNIT2_PATH or install shunit2." >&2
    exit 1
}

. "$SHUNIT2_PATH"

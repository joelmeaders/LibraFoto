#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

source "$THIS_DIR/test_helpers.sh"

INSTALL_SCRIPT="$ROOT_DIR/install.sh"

test_install_help_shows_usage() {
    local output
    output=$(bash "$INSTALL_SCRIPT" --help 2>&1)
    local status=$?

    assertEquals 0 $status
    echo "$output" | grep -q "Usage:" 
    assertEquals 0 $?
}

test_install_help_mentions_interactive_features() {
    local output
    output=$(bash "$INSTALL_SCRIPT" --help 2>&1)
    # Script is now interactive-first, check for key terms
    echo "$output" | grep -q "interactive"
    assertEquals "--help should mention interactive" 0 $?
}

test_install_help_mentions_deployment_method() {
    local output
    output=$(bash "$INSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "Deployment.*method\|GHCR.*images\|build from source"
    assertEquals "--help should mention deployment options" 0 $?
}

test_install_help_mentions_kiosk_mode() {
    local output
    output=$(bash "$INSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -qi "kiosk"
    assertEquals "--help should mention kiosk mode" 0 $?
}

test_install_rejects_unknown_option() {
    local output
    output=$(bash "$INSTALL_SCRIPT" --bogus-flag 2>&1)
    local status=$?

    assertNotEquals "unknown option should fail" 0 $status
    echo "$output" | grep -qi "unknown\|error"
    assertEquals "should mention unknown/error" 0 $?
}

SHUNIT2_PATH=$(find_shunit2) || {
    echo "shUnit2 not found. Set SHUNIT2_PATH or install shunit2." >&2
    exit 1
}

. "$SHUNIT2_PATH"

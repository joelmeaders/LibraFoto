#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

source "$THIS_DIR/test_helpers.sh"

UNINSTALL_SCRIPT="$ROOT_DIR/uninstall.sh"

test_uninstall_help_shows_usage() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    local status=$?

    assertEquals 0 $status
    echo "$output" | grep -q "Usage:" 
    assertEquals 0 $?
}

test_uninstall_help_mentions_interactive_features() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "interactive"
    assertEquals "--help should mention interactive" 0 $?
}

test_uninstall_help_mentions_confirmation() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "confirm\|asked"
    assertEquals "--help should mention confirmation" 0 $?
}

test_uninstall_help_mentions_images_removal() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -qi "images"
    assertEquals "--help should mention images removal" 0 $?
}

test_uninstall_help_mentions_data_preservation() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -qi "data\|photos"
    assertEquals "--help should mention data/photos" 0 $?
}

test_uninstall_rejects_unknown_option() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --bogus-flag 2>&1)
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

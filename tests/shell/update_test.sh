#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

source "$THIS_DIR/test_helpers.sh"

UPDATE_SCRIPT="$ROOT_DIR/update.sh"

test_update_help_shows_usage() {
    local output
    output=$(bash "$UPDATE_SCRIPT" --help 2>&1)
    local status=$?

    assertEquals 0 $status
    echo "$output" | grep -q "Usage:" 
    assertEquals 0 $?
}

test_update_help_mentions_check_flag() {
    local output
    output=$(bash "$UPDATE_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "\-\-check"
    assertEquals "--help should mention --check" 0 $?
}

test_update_help_mentions_list_backups_flag() {
    local output
    output=$(bash "$UPDATE_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "\-\-list-backups"
    assertEquals "--help should mention --list-backups" 0 $?
}

test_update_help_mentions_interactive_workflow() {
    local output
    output=$(bash "$UPDATE_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "interactive"
    assertEquals "--help should mention interactive workflow" 0 $?
}

test_update_rejects_unknown_option() {
    local output
    output=$(bash "$UPDATE_SCRIPT" --bogus-flag 2>&1)
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

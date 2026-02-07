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

test_uninstall_help_mentions_purge_flag() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "\-\-purge"
    assertEquals "--help should mention --purge" 0 $?
}

test_uninstall_help_mentions_force_flag() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "\-\-force"
    assertEquals "--help should mention --force" 0 $?
}

test_uninstall_help_mentions_dry_run_flag() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "\-\-dry-run"
    assertEquals "--help should mention --dry-run" 0 $?
}

test_uninstall_help_mentions_keep_docker_flag() {
    local output
    output=$(bash "$UNINSTALL_SCRIPT" --help 2>&1)
    echo "$output" | grep -q "\-\-keep-docker"
    assertEquals "--help should mention --keep-docker" 0 $?
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

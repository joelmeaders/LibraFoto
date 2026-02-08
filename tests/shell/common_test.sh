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

test_log_success_writes_ok_to_log() {
    log_init "Test Script"
    log_success "operation done" >/dev/null
    assertFileContains "$TEST_LOG_FILE" "[OK]"
    assertFileContains "$TEST_LOG_FILE" "operation done"
}

test_log_warn_writes_warn_to_log() {
    log_init "Test Script"
    log_warn "something fishy" >/dev/null
    assertFileContains "$TEST_LOG_FILE" "[WARN]"
    assertFileContains "$TEST_LOG_FILE" "something fishy"
}

test_log_error_writes_error_to_log() {
    log_init "Test Script"
    log_error "bad thing" 2>/dev/null
    assertFileContains "$TEST_LOG_FILE" "[ERROR]"
    assertFileContains "$TEST_LOG_FILE" "bad thing"
}

test_log_step_writes_step_header_to_log() {
    log_init "Test Script"
    log_step "2/5" "Docker Setup" >/dev/null
    assertFileContains "$TEST_LOG_FILE" "\\[2/5\\]"
    assertFileContains "$TEST_LOG_FILE" "Docker Setup"
}

test_confirm_prompt_defaults_yes_on_empty_input() {
    confirm_prompt "Continue?" "Y" <<< ""
    assertEquals 0 $?
}

test_confirm_prompt_defaults_no_when_default_is_N() {
    confirm_prompt "Continue?" "N" <<< ""
    assertNotEquals 0 $?
}

test_check_command_detects_existing_binary() {
    check_command bash
    assertEquals 0 $?
}

test_check_command_returns_nonzero_for_missing_binary() {
    check_command surely_no_such_command_exists_xyz
    assertNotEquals 0 $?
}

test_get_pi_user_defaults_to_pi() {
    unset SUDO_USER || true
    local user
    user=$(get_pi_user)
    assertEquals "pi" "$user"
}

test_get_pi_user_returns_sudo_user_when_set() {
    SUDO_USER="testuser"
    local user
    user=$(get_pi_user)
    assertEquals "testuser" "$user"
    unset SUDO_USER
}

test_get_current_version_reads_file() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    echo "2.5.1" > "$temp_dir/.version"
    local version
    version=$(get_current_version "$temp_dir")
    assertEquals "2.5.1" "$version"
}

test_get_current_version_returns_unknown_when_missing() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    local version
    version=$(get_current_version "$temp_dir")
    assertEquals "unknown" "$version"
}

test_show_banner_outputs_librafoto_text() {
    local output
    # Call without subtitle so the text line contains "LibraFoto v..."
    output=$(show_banner | cat -v)
    echo "$output" | grep -q "LibraFoto"
    assertEquals 0 $?
}

test_show_banner_includes_subtitle() {
    local output
    output=$(show_banner "My Subtitle" | cat -v)
    echo "$output" | grep -q "My Subtitle"
    assertEquals 0 $?
}

# =============================================================================
# find_librafoto_root tests
# =============================================================================

test_find_librafoto_root_finds_directory_with_compose() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/project/docker"
    touch "$temp_dir/project/docker/docker-compose.yml"
    mkdir -p "$temp_dir/project/sub/deep"

    local result
    result=$(find_librafoto_root "$temp_dir/project/sub/deep")
    assertEquals "$temp_dir/project" "$result"
}

test_find_librafoto_root_fails_when_no_compose() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/empty/sub"

    find_librafoto_root "$temp_dir/empty/sub"
    assertNotEquals 0 $?
}

# =============================================================================
# Deploy Mode utilities tests
# =============================================================================

test_read_env_var_reads_value() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"
    echo "MY_VAR=hello" > "$temp_dir/docker/.env"

    local val
    val=$(read_env_var "MY_VAR" "$temp_dir")
    assertEquals "hello" "$val"
}

test_read_env_var_returns_empty_for_missing_var() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"
    echo "OTHER=world" > "$temp_dir/docker/.env"

    local val
    val=$(read_env_var "MISSING_VAR" "$temp_dir")
    assertEquals "" "$val"
}

test_read_env_var_returns_empty_when_no_env_file() {
    local temp_dir
    temp_dir=$(make_temp_dir)

    local val
    val=$(read_env_var "ANY_VAR" "$temp_dir")
    assertEquals "" "$val"
}

test_get_deploy_mode_defaults_to_build() {
    local temp_dir
    temp_dir=$(make_temp_dir)

    local mode
    mode=$(get_deploy_mode "$temp_dir")
    assertEquals "build" "$mode"
}

test_get_deploy_mode_reads_release_from_env() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"
    echo "DEPLOY_MODE=release" > "$temp_dir/docker/.env"

    local mode
    mode=$(get_deploy_mode "$temp_dir")
    assertEquals "release" "$mode"
}

test_get_compose_file_returns_default_for_build_mode() {
    local temp_dir
    temp_dir=$(make_temp_dir)

    local file
    file=$(get_compose_file "$temp_dir")
    assertEquals "$temp_dir/docker/docker-compose.yml" "$file"
}

test_get_compose_file_returns_release_compose_for_release_mode() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"
    echo "DEPLOY_MODE=release" > "$temp_dir/docker/.env"

    local file
    file=$(get_compose_file "$temp_dir")
    assertEquals "$temp_dir/docker/docker-compose.release.yml" "$file"
}

test_get_compose_filename_returns_default_for_build() {
    local temp_dir
    temp_dir=$(make_temp_dir)

    local name
    name=$(get_compose_filename "$temp_dir")
    assertEquals "docker-compose.yml" "$name"
}

test_get_compose_filename_returns_release_for_release() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"
    echo "DEPLOY_MODE=release" > "$temp_dir/docker/.env"

    local name
    name=$(get_compose_filename "$temp_dir")
    assertEquals "docker-compose.release.yml" "$name"
}

# =============================================================================
# Version comparison tests
# =============================================================================

test_is_version_newer_detects_newer() {
    is_version_newer "1.0.0" "1.1.0"
    assertEquals 0 $?
}

test_is_version_newer_detects_same() {
    is_version_newer "1.0.0" "1.0.0"
    assertNotEquals 0 $?
}

test_is_version_newer_detects_older() {
    is_version_newer "1.1.0" "1.0.0"
    assertNotEquals 0 $?
}

test_is_version_newer_with_prerelease() {
    is_version_newer "0.1.0-alpha.1" "0.1.0-alpha.2"
    assertEquals 0 $?
}

# =============================================================================
# Release mode detection tests
# =============================================================================

test_is_release_mode_true_when_images_exist() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/images"
    touch "$temp_dir/images/librafoto-api.tar"

    ( cd "$temp_dir" && is_release_mode )
    assertEquals 0 $?
}

test_is_release_mode_false_when_no_images() {
    local temp_dir
    temp_dir=$(make_temp_dir)

    ( cd "$temp_dir" && is_release_mode )
    assertNotEquals 0 $?
}

# =============================================================================
# Architecture detection tests
# =============================================================================

test_detect_architecture_returns_value() {
    local arch
    arch=$(detect_architecture)
    # Should return either amd64 or arm64
    assertTrue "arch should be amd64 or arm64" "[ \"$arch\" = \"amd64\" ] || [ \"$arch\" = \"arm64\" ]"
}

# =============================================================================
# set_env_var tests
# =============================================================================

test_set_env_var_creates_new_file_and_var() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"

    set_env_var "NEW_VAR" "new_value" "$temp_dir"
    assertFileContains "$temp_dir/docker/.env" "NEW_VAR=new_value"
}

test_set_env_var_updates_existing_var() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"
    echo "MY_VAR=old_value" > "$temp_dir/docker/.env"

    set_env_var "MY_VAR" "new_value" "$temp_dir"
    assertFileContains "$temp_dir/docker/.env" "MY_VAR=new_value"

    # Ensure old value is gone
    ! grep -q "MY_VAR=old_value" "$temp_dir/docker/.env"
    assertEquals 0 $?
}

test_set_env_var_appends_when_var_not_present() {
    local temp_dir
    temp_dir=$(make_temp_dir)
    mkdir -p "$temp_dir/docker"
    echo "EXISTING=keep" > "$temp_dir/docker/.env"

    set_env_var "ADDED_VAR" "added_value" "$temp_dir"
    assertFileContains "$temp_dir/docker/.env" "EXISTING=keep"
    assertFileContains "$temp_dir/docker/.env" "ADDED_VAR=added_value"
}

SHUNIT2_PATH=$(find_shunit2) || {
    echo "shUnit2 not found. Set SHUNIT2_PATH or install shunit2." >&2
    exit 1
}

. "$SHUNIT2_PATH"

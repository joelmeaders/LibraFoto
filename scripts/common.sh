#!/bin/bash
#
# LibraFoto - Common Shell Script Helpers
#
# This script provides shared functionality for LibraFoto shell scripts:
# - Color constants for terminal output
# - Logging functions with file output
# - Common utility functions
# - Banner display
# - Docker and system utilities
#
# Usage (source this file from your script):
#   SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
#   source "$SCRIPT_DIR/scripts/common.sh" || source "$SCRIPT_DIR/common.sh"
#
# Before sourcing, you can set:
#   SCRIPT_VERSION - Version string for your script
#   SCRIPT_NAME    - Name of your script (for banner subtitle)
#   LOG_FILE       - Override default log file path
#
# Version: 1.0.0
# Repository: https://github.com/librafoto/librafoto

# Guard against double-sourcing
if [[ -n "${LIBRAFOTO_COMMON_SOURCED:-}" ]]; then
    return 0
fi
readonly LIBRAFOTO_COMMON_SOURCED=true

# =============================================================================
# Color Constants
# =============================================================================

# Only define if not already set (allows override)
readonly RED="${RED:-\033[0;31m}"
readonly GREEN="${GREEN:-\033[0;32m}"
readonly YELLOW="${YELLOW:-\033[1;33m}"
readonly BLUE="${BLUE:-\033[0;34m}"
readonly CYAN="${CYAN:-\033[0;36m}"
readonly BOLD="${BOLD:-\033[1m}"
readonly NC="${NC:-\033[0m}"  # No Color

# =============================================================================
# Logging Functions
# =============================================================================

# Initialize log file with header
# Usage: log_init "Script Name"
log_init() {
    local script_name="${1:-LibraFoto}"
    local log_file="${LOG_FILE:-/tmp/librafoto.log}"
    local version="${SCRIPT_VERSION:-1.0.0}"
    
    {
        echo "$script_name Log - $(date)"
        echo "Script Version: $version"
        echo "=========================================="
    } > "$log_file"
}

# Log an info message
log_info() {
    local message="$1"
    local log_file="${LOG_FILE:-/tmp/librafoto.log}"
    echo -e "${BLUE}[INFO]${NC} $message"
    echo "[INFO] $(date '+%H:%M:%S') $message" >> "$log_file"
}

# Log a success message
log_success() {
    local message="$1"
    local log_file="${LOG_FILE:-/tmp/librafoto.log}"
    echo -e "${GREEN}[OK]${NC} $message"
    echo "[OK] $(date '+%H:%M:%S') $message" >> "$log_file"
}

# Log a warning message
log_warn() {
    local message="$1"
    local log_file="${LOG_FILE:-/tmp/librafoto.log}"
    echo -e "${YELLOW}[WARN]${NC} $message"
    echo "[WARN] $(date '+%H:%M:%S') $message" >> "$log_file"
}

# Log an error message (to stderr)
log_error() {
    local message="$1"
    local log_file="${LOG_FILE:-/tmp/librafoto.log}"
    echo -e "${RED}[ERROR]${NC} $message" >&2
    echo "[ERROR] $(date '+%H:%M:%S') $message" >> "$log_file"
}

# Log a step/section header
# Usage: log_step "1/5" "Step Description"
log_step() {
    local step="$1"
    local message="$2"
    local log_file="${LOG_FILE:-/tmp/librafoto.log}"
    echo ""
    echo -e "${CYAN}${BOLD}[$step]${NC} ${BOLD}$message${NC}"
    {
        echo ""
        echo "[$step] $message"
    } >> "$log_file"
}

# =============================================================================
# Utility Functions
# =============================================================================

# Check if a command exists
check_command() {
    command -v "$1" &> /dev/null
}

# Get the user who invoked sudo, fallback to 'pi'
get_pi_user() {
    echo "${SUDO_USER:-pi}"
}

# Get the home directory for the Pi user
get_pi_home() {
    local user
    user=$(get_pi_user)
    eval echo "~$user"
}

# Get the primary IP address
get_ip_address() {
    hostname -I 2>/dev/null | awk '{print $1}' || echo "localhost"
}

# Display a confirmation prompt
# Usage: confirm_prompt "Continue?" "Y"  # Y or N for default
# Returns: 0 if yes, 1 if no
confirm_prompt() {
    local message="$1"
    local default="${2:-Y}"
    local prompt
    local response
    
    if [[ "$default" == "Y" ]]; then
        prompt="[Y/n]"
    else
        prompt="[y/N]"
    fi
    
    echo -en "${BOLD}$message $prompt${NC} "
    read -r response
    
    if [[ -z "$response" ]]; then
        response="$default"
    fi
    
    [[ "$response" =~ ^[Yy]$ ]]
}

# Display a spinner while a process runs
# Usage: show_spinner $PID "Loading..."
show_spinner() {
    local pid=$1
    local message="$2"
    local spin='⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏'
    local i=0
    
    while kill -0 "$pid" 2>/dev/null; do
        printf "\r${BLUE}[%s]${NC} %s" "${spin:i++%${#spin}:1}" "$message"
        sleep 0.1
    done
    printf "\r"
}

# Check if running as root
check_root() {
    if [[ $EUID -ne 0 ]]; then
        log_error "This script must be run with sudo"
        echo "Usage: sudo $0"
        exit 1
    fi
}

# =============================================================================
# Docker Utilities
# =============================================================================

# Check if Docker is available and running
check_docker() {
    if ! check_command docker; then
        log_warn "Docker not found"
        return 1
    fi
    
    if ! docker info &>/dev/null; then
        log_warn "Docker daemon not running or insufficient permissions"
        return 1
    fi
    
    return 0
}

# Get the current version from .version file
get_current_version() {
    local script_dir="${1:-.}"
    
    if [[ -f "$script_dir/.version" ]]; then
        cat "$script_dir/.version"
    else
        echo "unknown"
    fi
}

# Get the current git commit hash (short)
get_current_commit() {
    git rev-parse --short HEAD 2>/dev/null || echo "unknown"
}

# =============================================================================
# Banner Display
# =============================================================================

# Display the LibraFoto ASCII art banner with subtitle
# Usage: show_banner "Installation Script"
show_banner() {
    local subtitle="${1:-}"
    local version="${SCRIPT_VERSION:-1.0.0}"
    
    echo -e "${CYAN}"
    cat << 'EOF'
  _      _ _               _____    _        
 | |    (_) |             |  ___|  | |       
 | |     _| |__  _ __ __ _| |_ ___ | |_ ___  
 | |    | | '_ \| '__/ _` |  _/ _ \| __/ _ \ 
 | |____| | |_) | | | (_| | || (_) | || (_) |
 |______|_|_.__/|_|  \__,_\_| \___/ \__\___/ 
                                              
EOF
    echo -e "${NC}"
    
    if [[ -n "$subtitle" ]]; then
        echo -e "${BOLD}${subtitle} v${version}${NC}"
    else
        echo -e "${BOLD}LibraFoto v${version}${NC}"
    fi
    echo ""
}

# =============================================================================
# Raspberry Pi Utilities
# =============================================================================

# Check if running on a Raspberry Pi
is_raspberry_pi() {
    if [[ ! -f /proc/cpuinfo ]]; then
        return 1
    fi
    
    grep -qiE "Raspberry Pi|BCM2711|BCM2712" /proc/cpuinfo 2>/dev/null
}

# Get Raspberry Pi model name
get_pi_model() {
    if [[ -f /proc/cpuinfo ]]; then
        grep -E "^Model" /proc/cpuinfo | cut -d: -f2 | xargs || echo "Unknown"
    else
        echo "Unknown"
    fi
}

# Check if running on 64-bit architecture
is_64bit() {
    [[ "$(uname -m)" == "aarch64" ]]
}

# Get total RAM in MB
get_total_ram_mb() {
    local total_mem_kb
    total_mem_kb=$(grep MemTotal /proc/meminfo 2>/dev/null | awk '{print $2}')
    echo $((total_mem_kb / 1024))
}

# Get available disk space in GB
get_available_disk_gb() {
    df -BG "${1:-.}" 2>/dev/null | tail -1 | awk '{print $4}' | tr -d 'G'
}

# =============================================================================
# Path Utilities
# =============================================================================

# Find the LibraFoto root directory (contains docker/docker-compose.yml)
# Usage: find_librafoto_root "/some/starting/path"
find_librafoto_root() {
    local start_path="${1:-$(pwd)}"
    local current="$start_path"
    
    while [[ "$current" != "/" ]]; do
        if [[ -f "$current/docker/docker-compose.yml" ]]; then
            echo "$current"
            return 0
        fi
        current="$(dirname "$current")"
    done
    
    return 1
}

# =============================================================================
# Deploy Mode Utilities
# =============================================================================

# GitHub repository for release downloads
readonly GITHUB_REPO="librafoto/librafoto"

# Read a variable from docker/.env file
# Usage: read_env_var "DEPLOY_MODE" "/path/to/librafoto"
read_env_var() {
    local var_name="$1"
    local root_dir="${2:-.}"
    local env_file="$root_dir/docker/.env"

    if [[ -f "$env_file" ]]; then
        grep -E "^${var_name}=" "$env_file" 2>/dev/null | tail -1 | cut -d= -f2-
    fi
}

# Get the current deploy mode (build or release)
# Usage: get_deploy_mode "/path/to/librafoto"
get_deploy_mode() {
    local root_dir="${1:-.}"
    local mode
    mode=$(read_env_var "DEPLOY_MODE" "$root_dir")
    echo "${mode:-build}"
}

# Get the compose file path based on deploy mode
# Usage: get_compose_file "/path/to/librafoto"
get_compose_file() {
    local root_dir="${1:-.}"
    local mode
    mode=$(get_deploy_mode "$root_dir")

    if [[ "$mode" == "release" ]]; then
        echo "$root_dir/docker/docker-compose.release.yml"
    else
        echo "$root_dir/docker/docker-compose.yml"
    fi
}

# Get the compose filename only (for use when already in docker/ dir)
# Usage: get_compose_filename "/path/to/librafoto"
get_compose_filename() {
    local root_dir="${1:-.}"
    local mode
    mode=$(get_deploy_mode "$root_dir")

    if [[ "$mode" == "release" ]]; then
        echo "docker-compose.release.yml"
    else
        echo "docker-compose.yml"
    fi
}

# Update or add a variable in docker/.env file
# Usage: set_env_var "DEPLOY_MODE" "release" "/path/to/librafoto"
set_env_var() {
    local var_name="$1"
    local var_value="$2"
    local root_dir="${3:-.}"
    local env_file="$root_dir/docker/.env"

    if [[ ! -f "$env_file" ]]; then
        echo "${var_name}=${var_value}" > "$env_file"
        return
    fi

    if grep -qE "^${var_name}=" "$env_file" 2>/dev/null; then
        # Update existing variable
        sed -i "s|^${var_name}=.*|${var_name}=${var_value}|" "$env_file"
    else
        # Append new variable
        echo "${var_name}=${var_value}" >> "$env_file"
    fi
}

# =============================================================================
# Release Mode Utilities
# =============================================================================

# Detect CPU architecture and return docker-style name
# Returns: arm64 or amd64
detect_architecture() {
    local arch
    arch=$(uname -m)
    case "$arch" in
        aarch64|arm64) echo "arm64" ;;
        x86_64|amd64)  echo "amd64" ;;
        *)             echo "$arch"  ;;
    esac
}

# Check if the install directory contains pre-built release images
# Usage: is_release_mode "/path/to/librafoto"
is_release_mode() {
    local root_dir="${1:-.}"
    local images_dir="$root_dir/images"

    if [[ -d "$images_dir" ]] && ls "$images_dir"/*.tar &>/dev/null; then
        return 0
    fi
    return 1
}

# Load Docker images from tar files in a directory
# Usage: load_images_from_dir "/path/to/librafoto"
load_images_from_dir() {
    local root_dir="${1:-.}"
    local images_dir="$root_dir/images"
    local loaded=0
    local failed=0

    if [[ ! -d "$images_dir" ]]; then
        log_error "Images directory not found: $images_dir"
        return 1
    fi

    for tar_file in "$images_dir"/*.tar; do
        if [[ ! -f "$tar_file" ]]; then
            continue
        fi

        local filename
        filename=$(basename "$tar_file")
        log_info "Loading image: $filename..."

        if docker load -i "$tar_file" >> "${LOG_FILE:-/tmp/librafoto.log}" 2>&1; then
            log_success "Loaded: $filename"
            ((loaded++))
        else
            log_error "Failed to load: $filename"
            ((failed++))
        fi
    done

    if [[ $failed -gt 0 ]]; then
        log_error "$failed image(s) failed to load"
        return 1
    fi

    if [[ $loaded -eq 0 ]]; then
        log_error "No .tar image files found in $images_dir"
        return 1
    fi

    log_success "$loaded image(s) loaded successfully"
    return 0
}

# Query GitHub Releases API for the latest release
# Outputs two lines: VERSION and DOWNLOAD_URL for the current platform's zip
# Usage: eval "$(get_github_latest_release)"
#   Then use: $RELEASE_VERSION, $RELEASE_DOWNLOAD_URL, $RELEASE_BODY
get_github_latest_release() {
    local arch
    arch=$(detect_architecture)
    local api_url="https://api.github.com/repos/${GITHUB_REPO}/releases/latest"

    local response
    response=$(curl -fsSL --connect-timeout 10 "$api_url" 2>/dev/null) || {
        echo "RELEASE_VERSION=\"\""
        echo "RELEASE_DOWNLOAD_URL=\"\""
        echo "RELEASE_BODY=\"\""
        return 1
    }

    # Extract tag name (remove leading 'v')
    local tag
    tag=$(echo "$response" | grep '"tag_name"' | head -1 | sed 's/.*"tag_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/')
    local version="${tag#v}"

    # Extract download URL for this platform's zip
    local download_url
    download_url=$(echo "$response" | grep '"browser_download_url"' | grep "${arch}.zip" | head -1 | sed 's/.*"browser_download_url"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/')

    # Extract release body (first 500 chars for changelog preview)
    local body
    body=$(echo "$response" | grep '"body"' | head -1 | sed 's/.*"body"[[:space:]]*:[[:space:]]*"\(.*\)".*/\1/' | head -c 500)

    echo "RELEASE_VERSION=\"$version\""
    echo "RELEASE_DOWNLOAD_URL=\"$download_url\""
    echo "RELEASE_BODY=\"$body\""
}

# Download and extract a release zip to a temporary directory
# Usage: download_release_zip "https://.../.zip" "/tmp/librafoto-update"
# Returns: 0 on success, 1 on failure
download_release_zip() {
    local url="$1"
    local dest_dir="$2"

    if [[ -z "$url" ]]; then
        log_error "No download URL provided"
        return 1
    fi

    mkdir -p "$dest_dir"
    local zip_file="$dest_dir/librafoto-release.zip"

    log_info "Downloading release from GitHub..."
    if ! curl -fSL --progress-bar --connect-timeout 30 -o "$zip_file" "$url"; then
        log_error "Download failed"
        return 1
    fi

    log_info "Extracting release archive..."
    if ! unzip -qo "$zip_file" -d "$dest_dir" 2>> "${LOG_FILE:-/tmp/librafoto.log}"; then
        log_error "Failed to extract archive"
        return 1
    fi

    # Clean up the zip file to save space
    rm -f "$zip_file"

    log_success "Release downloaded and extracted to $dest_dir"
    return 0
}

# Compare two semver version strings
# Returns: 0 if v1 < v2, 1 if v1 >= v2
# Usage: is_version_newer "1.0.0" "1.1.0" && echo "update available"
is_version_newer() {
    local current="$1"
    local remote="$2"

    if [[ "$current" == "$remote" ]]; then
        return 1
    fi

    # Use sort -V for version comparison
    local oldest
    oldest=$(printf '%s\n%s' "$current" "$remote" | sort -V | head -1)

    if [[ "$oldest" == "$current" ]]; then
        return 0  # current is older, update available
    fi
    return 1  # current is same or newer
}

# =============================================================================
# Internet Connectivity
# =============================================================================

# Check if internet is available
check_internet() {
    if ping -c 1 -W 5 google.com &>/dev/null || \
       ping -c 1 -W 5 github.com &>/dev/null; then
        return 0
    fi
    return 1
}

# =============================================================================
# Resource Management & Tracking
# =============================================================================

# Global arrays for operation tracking (declare once)
declare -a TRACKED_OPERATIONS=()
declare -a TRACKED_RESULTS=()
declare -a TRACKED_ERRORS=()

# List Docker resources (containers, volumes, networks, images)
# Usage: list_docker_resources "/path/to/librafoto"
list_docker_resources() {
    local root_dir="${1:-.}"
    local compose_file
    compose_file=$(get_compose_file "$root_dir")
    
    echo -e "\n${BOLD}Docker Resources:${NC}"
    
    if check_docker && [[ -f "$compose_file" ]]; then
        echo -e "\n  ${CYAN}Containers:${NC}"
        if docker compose -f "$compose_file" ps --format "table {{.Name}}\t{{.Status}}" 2>/dev/null | tail -n +2 | grep -q .; then
            docker compose -f "$compose_file" ps --format "    {{.Name}} - {{.Status}}" 2>/dev/null
        else
            echo "    (none running)"
        fi
        
        echo -e "\n  ${CYAN}Volumes:${NC}"
        if docker volume ls --format "{{.Name}}" | grep -iE "librafoto|docker_" | grep -q .; then
            docker volume ls --format "{{.Name}}" | grep -iE "librafoto|docker_" | sed 's/^/    /'
        else
            echo "    (none found)"
        fi
        
        echo -e "\n  ${CYAN}Networks:${NC}"
        if docker network ls --format "{{.Name}}" | grep -iE "librafoto|docker_" | grep -q .; then
            docker network ls --format "{{.Name}}" | grep -iE "librafoto|docker_" | sed 's/^/    /'
        else
            echo "    (none found)"
        fi
        
        echo -e "\n  ${CYAN}Images:${NC}"
        if docker images --format "{{.Repository}}:{{.Tag}}" | grep -iE "librafoto|docker-" | grep -q .; then
            docker images --format "    {{.Repository}}:{{.Tag}} ({{.Size}})" | grep -iE "librafoto|docker-"
        else
            echo "    (none found)"
        fi
    else
        echo "    (Docker not available or compose file not found)"
    fi
}

# List kiosk-related files and services
list_kiosk_files() {
    local pi_home
    pi_home=$(get_pi_home)
    
    echo -e "\n${BOLD}Kiosk Configuration:${NC}"
    
    local files=(
        "$pi_home/start-kiosk.sh"
        "$pi_home/.config/autostart/librafoto-kiosk.desktop"
        "/etc/xdg/lxsession/LXDE-pi/autostart"
        "$pi_home/.config/lxsession/LXDE-pi/autostart"
        "/etc/lightdm/lightdm.conf"
    )
    
    local found_any=false
    for file in "${files[@]}"; do
        if [[ -f "$file" ]]; then
            echo "    ✓ $file"
            found_any=true
        fi
    done
    
    # Check for autostart entries
    if grep -q "start-kiosk.sh\|LibraFoto" /etc/xdg/lxsession/LXDE-pi/autostart 2>/dev/null || \
       grep -q "start-kiosk.sh\|LibraFoto" "$pi_home/.config/lxsession/LXDE-pi/autostart" 2>/dev/null; then
        echo "    ✓ Autostart entries configured"
        found_any=true
    fi
    
    # Check for systemd services
    if systemctl list-unit-files "librafoto*.service" 2>/dev/null | grep -q librafoto; then
        echo -e "\n  ${CYAN}Systemd Services:${NC}"
        systemctl list-unit-files "librafoto*.service" --no-pager 2>/dev/null | grep librafoto | sed 's/^/    /'
        found_any=true
    fi
    
    if [[ "$found_any" == false ]]; then
        echo "    (no kiosk configuration found)"
    fi
}

# List configuration files and directories
# Usage: list_config_files "/path/to/librafoto"
list_config_files() {
    local root_dir="${1:-.}"
    
    echo -e "\n${BOLD}Configuration & Data:${NC}"
    
    if [[ -f "$root_dir/docker/.env" ]]; then
        echo "    ✓ $root_dir/docker/.env"
    else
        echo "    ✗ $root_dir/docker/.env (not created yet)"
    fi
    
    if [[ -d "$root_dir/data" ]]; then
        local size
        size=$(du -sh "$root_dir/data" 2>/dev/null | cut -f1 || echo "unknown")
        echo "    ✓ $root_dir/data/ ($size)"
    else
        echo "    ✗ $root_dir/data/ (not created yet)"
    fi
    
    if [[ -d "$root_dir/backups" ]]; then
        local count
        count=$(find "$root_dir/backups" -mindepth 1 -maxdepth 1 -type d 2>/dev/null | wc -l)
        echo "    ✓ $root_dir/backups/ ($count backup(s))"
    else
        echo "    ✗ $root_dir/backups/ (not created yet)"
    fi
}

# Track an operation for later summary
# Usage: track_operation "Stop containers" "docker compose down"
# Returns: exit code of the command
track_operation() {
    local description="$1"
    shift
    local command=("$@")
    
    # Execute the command and capture result
    local output
    local exit_code
    if output=$("${command[@]}" 2>&1); then
        exit_code=0
    else
        exit_code=$?
    fi
    
    # Store the operation
    TRACKED_OPERATIONS+=("$description")
    TRACKED_RESULTS+=("$exit_code")
    if [[ $exit_code -ne 0 ]]; then
        TRACKED_ERRORS+=("$output")
    else
        TRACKED_ERRORS+=("")
    fi
    
    return $exit_code
}

# Show summary of all tracked operations
show_operation_summary() {
    local success_count=0
    local failure_count=0
    
    echo -e "\n${BOLD}═══════════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}Operation Summary${NC}"
    echo -e "${BOLD}═══════════════════════════════════════════════════════${NC}\n"
    
    for i in "${!TRACKED_OPERATIONS[@]}"; do
        local description="${TRACKED_OPERATIONS[$i]}"
        local result="${TRACKED_RESULTS[$i]}"
        local error="${TRACKED_ERRORS[$i]}"
        
        if [[ "$result" -eq 0 ]]; then
            echo -e "  ${GREEN}✓${NC} $description"
            ((success_count++))
        else
            echo -e "  ${RED}✗${NC} $description"
            if [[ -n "$error" ]]; then
                echo -e "    ${YELLOW}Error:${NC} $(echo "$error" | head -1)"
            fi
            ((failure_count++))
        fi
    done
    
    echo -e "\n${BOLD}Results:${NC} ${GREEN}${success_count} succeeded${NC}, ${RED}${failure_count} failed${NC}\n"
    
    if [[ $failure_count -gt 0 ]]; then
        echo -e "${YELLOW}Some operations failed. Items may need manual cleanup.${NC}"
        return 1
    fi
    return 0
}

# Validate that resources were actually removed
# Usage: validate_removal "containers" "volumes" "kiosk"
validate_removal() {
    local root_dir
    root_dir=$(find_librafoto_root "$(pwd)") || root_dir="."
    local failed_validations=()
    
    for resource_type in "$@"; do
        case "$resource_type" in
            containers)
                if check_docker; then
                    local compose_file
                    compose_file=$(get_compose_file "$root_dir")
                    if docker compose -f "$compose_file" ps -q 2>/dev/null | grep -q .; then
                        failed_validations+=("Containers still running")
                    fi
                fi
                ;;
            volumes)
                if check_docker; then
                    if docker volume ls --format "{{.Name}}" | grep -qiE "librafoto|docker_"; then
                        failed_validations+=("Volumes still exist")
                    fi
                fi
                ;;
            images)
                if check_docker; then
                    if docker images --format "{{.Repository}}" | grep -qiE "librafoto|docker-"; then
                        failed_validations+=("Images still exist")
                    fi
                fi
                ;;
            kiosk)
                local pi_home
                pi_home=$(get_pi_home)
                if [[ -f "$pi_home/start-kiosk.sh" ]] || \
                   [[ -f "$pi_home/.config/autostart/librafoto-kiosk.desktop" ]] || \
                   systemctl list-unit-files "librafoto*.service" 2>/dev/null | grep -q librafoto; then
                    failed_validations+=("Kiosk files/services still exist")
                fi
                ;;
            config)
                if [[ -f "$root_dir/docker/.env" ]]; then
                    failed_validations+=("Configuration files still exist")
                fi
                ;;
            data)
                if [[ -d "$root_dir/data" ]] || [[ -d "$root_dir/backups" ]]; then
                    failed_validations+=("Data directories still exist")
                fi
                ;;
        esac
    done
    
    if [[ ${#failed_validations[@]} -gt 0 ]]; then
        for failure in "${failed_validations[@]}"; do
            echo "$failure"
        done
        return 1
    fi
    return 0
}

# Reset tracked operations (call at start of script if needed)
reset_operation_tracking() {
    TRACKED_OPERATIONS=()
    TRACKED_RESULTS=()
    TRACKED_ERRORS=()
}

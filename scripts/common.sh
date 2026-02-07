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

# Get the current deploy mode (build or ghcr)
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

    if [[ "$mode" == "ghcr" ]]; then
        echo "$root_dir/docker/docker-compose.ghcr.yml"
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

    if [[ "$mode" == "ghcr" ]]; then
        echo "docker-compose.ghcr.yml"
    else
        echo "docker-compose.yml"
    fi
}

# Check if a version string is a pre-release
# Matches: 1.0.0-alpha.1, 1.0.0-beta.2, 1.0.0-rc.1
# Usage: is_prerelease_version "0.1.0-alpha.1" && echo "prerelease"
is_prerelease_version() {
    local version="$1"
    [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+-(alpha|beta|rc)\.[0-9]+$ ]]
}

# Get the GHCR image tag for a given version
# Stable versions (1.0.0) -> "latest"
# Pre-release versions (0.1.0-alpha.1) -> "0.1.0-alpha.1"
# Usage: get_image_tag_for_version "0.1.0-alpha.1"
get_image_tag_for_version() {
    local version="$1"
    if is_prerelease_version "$version"; then
        echo "$version"
    else
        echo "latest"
    fi
}

# Update or add a variable in docker/.env file
# Usage: set_env_var "DEPLOY_MODE" "ghcr" "/path/to/librafoto"
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

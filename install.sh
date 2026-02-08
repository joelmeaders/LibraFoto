#!/bin/bash
#
# LibraFoto - Raspberry Pi Installation Script
# 
# This script automates the complete setup of LibraFoto on a Raspberry Pi:
# - System validation (Pi 4+, 64-bit OS, 2GB+ RAM)
# - Docker and Docker Compose installation
# - Kiosk mode configuration (Chromium fullscreen)
# - Container deployment
#
# Usage:
#   sudo ./install.sh           # Interactive installation
#   sudo ./install.sh --help    # Show help
#
# Requirements:
#   - Raspberry Pi 4 or later
#   - Raspberry Pi OS 64-bit with Desktop
#   - Internet connection
#   - Run from cloned LibraFoto repository
#
# Version: 1.0.0
# Repository: https://github.com/librafoto/librafoto

set -euo pipefail

# =============================================================================
# Configuration
# =============================================================================

readonly SCRIPT_VERSION="1.0.0"
readonly LOG_FILE="/tmp/librafoto-install.log"
readonly DISPLAY_URL="http://localhost/display/"
readonly MIN_RAM_MB=1800  # ~2GB with some tolerance
readonly REQUIRED_PI_MODELS="BCM2711|BCM2712"  # Pi 4 and Pi 5

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export LIBRAFOTO_DIR="$SCRIPT_DIR"

# =============================================================================
# Source Common Helpers
# =============================================================================

COMMON_SCRIPT="$SCRIPT_DIR/scripts/common.sh"
if [[ -f "$COMMON_SCRIPT" ]]; then
    source "$COMMON_SCRIPT"
else
    echo "Error: common.sh not found at $COMMON_SCRIPT"
    exit 1
fi

# Source kiosk setup functions
KIOSK_SCRIPT="$SCRIPT_DIR/scripts/kiosk-setup.sh"
if [[ -f "$KIOSK_SCRIPT" ]]; then
    source "$KIOSK_SCRIPT"
else
    echo "Warning: kiosk-setup.sh not found at $KIOSK_SCRIPT"
fi

# =============================================================================
# Banner and Help
# =============================================================================

# Override show_banner for install-specific subtitle
show_install_banner() {
    show_banner "Raspberry Pi Installation Script"
}

show_help() {
    show_install_banner
    cat << EOF
Usage: sudo $0 [OPTIONS]

OPTIONS:
    --help, -h      Show this help message

DESCRIPTION:
    This script automates the complete setup of LibraFoto on a Raspberry Pi,
    including Docker installation, kiosk mode configuration, and container
    deployment.

    There are two ways to install LibraFoto:

    1. Release zip (recommended): Download the release zip from GitHub,
       extract it, and run this script. Pre-built container images are
       included — no compilation or internet needed for deployment.

    2. Clone & build: Clone the git repository and run this script.
       Container images will be built locally from source code.

    The script auto-detects which mode to use based on whether pre-built
    images are present in the images/ directory.

    You'll also be prompted for kiosk mode configuration (fullscreen
    slideshow on boot).

REQUIREMENTS:
    - Raspberry Pi 4 or later
    - Raspberry Pi OS 64-bit with Desktop (Bookworm or later)
    - Internet connection (for Docker install; not needed if using release zip)
    - At least 2GB RAM

EXAMPLES:
    sudo ./install.sh                # Interactive installation

LOG FILE:
    Installation log is saved to: $LOG_FILE

EOF
}

show_install_preview() {
    local script_dir="${1:-.}"
    local deploy_mode="${2:-build}"
    local pi_home
    pi_home=$(get_pi_home)
    
    echo ""
    echo -e "${BOLD}═══════════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}Installation Preview${NC}"
    echo -e "${BOLD}═══════════════════════════════════════════════════════${NC}"
    
    echo -e "\n${BOLD}Deploy Mode:${NC} $([ "$deploy_mode" == "release" ] && echo "Pre-built images (from release zip)" || echo "Build from source (cloned repo)")"
    
    echo -e "\n${BOLD}System Modifications:${NC}"
    echo "    • Docker and Docker Compose (if not installed)"
    echo "    • User added to 'docker' group"
    if [[ "$deploy_mode" == "build" ]]; then
        echo "    • Swap file increased to 2GB (for building images)"
    fi
    
    echo -e "\n${BOLD}Directories to Create:${NC}"
    echo "    • $script_dir/data/ (photos and database, mode 755)"
    
    echo -e "\n${BOLD}Files to Create:${NC}"
    echo "    • $script_dir/docker/.env (environment configuration)"
    echo "      - LIBRAFOTO_HOST_IP (auto-detected)"
    echo "      - JWT_KEY (generated securely)"
    echo "      - VERSION (from .version file)"
    echo "      - DEPLOY_MODE ($deploy_mode)"
    
    echo -e "\n${BOLD}Docker Resources (from compose file):${NC}"
    echo "    • Containers: librafoto-api, librafoto-admin, librafoto-display, librafoto-proxy"
    echo "    • Network: librafoto_default"
    echo "    • Volume: librafoto-data"
    if [[ "$deploy_mode" == "release" ]]; then
        echo "    • Images: loaded from pre-built tar files"
    else
        echo "    • Images: built locally from source code"
    fi
    
    echo -e "\n${BOLD}Kiosk Mode Files (if enabled):${NC}"
    echo "    • $pi_home/start-kiosk.sh (startup script)"
    echo "    • $pi_home/.config/autostart/librafoto-kiosk.desktop (XDG autostart)"
    echo "    • /etc/systemd/system/librafoto-ip-update.service (IP update service)"
    echo "    • /etc/lightdm/lightdm.conf (modified for auto-login, backed up)"
    echo "    • /etc/xdg/lxsession/LXDE-pi/autostart (modified, backed up)"
    echo "    • $pi_home/.config/lxsession/LXDE-pi/autostart (configured)"
    
    echo -e "\n${BOLD}System Packages (if kiosk enabled):${NC}"
    echo "    • chromium-browser, unclutter, xdotool"
    
    echo -e "\n${BOLD}═══════════════════════════════════════════════════════${NC}\n"
}

# =============================================================================
# System Validation
# =============================================================================

check_raspberry_pi() {
    log_info "Checking for Raspberry Pi..."
    
    if [[ ! -f /proc/cpuinfo ]]; then
        log_error "Cannot detect CPU info. Are you running on Linux?"
        return 1
    fi
    
    local hardware
    hardware=$(grep -E "^Hardware|^Model" /proc/cpuinfo 2>/dev/null || true)
    
    if ! grep -qE "$REQUIRED_PI_MODELS" /proc/cpuinfo 2>/dev/null; then
        # Also check for "Raspberry Pi" in model name for newer kernels
        if ! grep -qi "Raspberry Pi [45]" /proc/cpuinfo 2>/dev/null; then
            log_error "Raspberry Pi 4 or later is required"
            log_info "Detected: $hardware"
            return 1
        fi
    fi
    
    local model
    model=$(grep -E "^Model" /proc/cpuinfo | cut -d: -f2 | xargs || echo "Unknown")
    log_success "Detected: $model"
    return 0
}

check_architecture() {
    log_info "Checking system architecture..."
    
    local arch
    arch=$(uname -m)
    
    if [[ "$arch" != "aarch64" ]]; then
        log_error "64-bit OS required (aarch64), but detected: $arch"
        log_info "Please install Raspberry Pi OS 64-bit"
        return 1
    fi
    
    log_success "Architecture: $arch (64-bit)"
    return 0
}

check_memory() {
    log_info "Checking available RAM..."
    
    local total_mem_kb
    total_mem_kb=$(grep MemTotal /proc/meminfo | awk '{print $2}')
    local total_mem_mb=$((total_mem_kb / 1024))
    
    if [[ $total_mem_mb -lt $MIN_RAM_MB ]]; then
        log_error "At least 2GB RAM required, but only ${total_mem_mb}MB detected"
        return 1
    fi
    
    log_success "RAM: ${total_mem_mb}MB available"
    return 0
}

check_disk_space() {
    log_info "Checking disk space..."
    
    local available_gb
    available_gb=$(df -BG . | tail -1 | awk '{print $4}' | tr -d 'G')
    
    if [[ $available_gb -lt 5 ]]; then
        log_warn "Low disk space: ${available_gb}GB available (5GB+ recommended)"
    else
        log_success "Disk space: ${available_gb}GB available"
    fi
    
    return 0
}

check_repo_files() {
    log_info "Checking repository files..."
    
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    if [[ "${INSTALL_DEPLOY_MODE:-}" == "release" ]]; then
        # Release mode: need compose file and image tars
        if [[ ! -f "$script_dir/docker/docker-compose.release.yml" ]]; then
            log_error "docker/docker-compose.release.yml not found"
            log_info "This doesn't appear to be a valid LibraFoto release"
            return 1
        fi
        if ! is_release_mode "$script_dir"; then
            log_error "Pre-built images not found in images/ directory"
            log_info "Please ensure you extracted the complete release zip"
            return 1
        fi
    else
        # Build mode: need build compose file and Dockerfiles
        if [[ ! -f "$script_dir/docker/docker-compose.yml" ]]; then
            log_error "docker/docker-compose.yml not found"
            log_info "Please run this script from the cloned LibraFoto repository"
            return 1
        fi
        if [[ ! -f "$script_dir/docker/Dockerfile.api" ]]; then
            log_error "Source files not found"
            log_info "Please ensure you have cloned the complete repository"
            return 1
        fi
    fi
    
    log_success "Repository files verified"
    return 0
}

check_internet_connectivity() {
    log_info "Checking internet connectivity..."
    
    if ! check_internet; then
        if [[ "${INSTALL_DEPLOY_MODE:-}" == "release" ]]; then
            log_warn "No internet connection detected (may be needed for Docker install)"
            return 0
        fi
        log_error "No internet connection detected"
        log_info "Internet is required for Docker installation and image builds"
        return 1
    fi
    
    log_success "Internet connection available"
    return 0
}

run_system_checks() {
    log_step "1/6" "System Validation"
    
    local checks_passed=true
    
    check_raspberry_pi || checks_passed=false
    check_architecture || checks_passed=false
    check_memory || checks_passed=false
    check_disk_space || true  # Warning only
    check_repo_files || checks_passed=false
    check_internet_connectivity || checks_passed=false
    
    if [[ "$checks_passed" != "true" ]]; then
        log_error "System validation failed. Please resolve the issues above."
        exit 1
    fi
    
    log_success "All system checks passed"
}

# =============================================================================
# Docker Installation
# =============================================================================

install_docker() {
    log_step "2/6" "Docker Installation"
    
    local pi_user
    pi_user=$(get_pi_user)
    
    # Check if Docker is already installed
    if check_command docker; then
        local docker_version
        docker_version=$(docker --version 2>/dev/null || echo "unknown")
        log_success "Docker already installed: $docker_version"
    else
        log_info "Installing Docker..."
        
        # Download and run official Docker install script
        curl -fsSL https://get.docker.com -o /tmp/get-docker.sh
        chmod +x /tmp/get-docker.sh
        
        if ! sh /tmp/get-docker.sh >> "$LOG_FILE" 2>&1; then
            log_error "Docker installation failed. Check $LOG_FILE for details."
            exit 1
        fi
        
        rm -f /tmp/get-docker.sh
        log_success "Docker installed successfully"
    fi
    
    # Add user to docker group
    if ! groups "$pi_user" | grep -q docker; then
        log_info "Adding $pi_user to docker group..."
        usermod -aG docker "$pi_user"
        log_success "User $pi_user added to docker group"
        log_warn "You may need to log out and back in for group changes to take effect"
    else
        log_success "User $pi_user already in docker group"
    fi
    
    # Enable Docker service
    log_info "Enabling Docker service..."
    systemctl enable docker >> "$LOG_FILE" 2>&1
    systemctl start docker >> "$LOG_FILE" 2>&1
    log_success "Docker service enabled and started"
    
    # Check for Docker Compose plugin
    if docker compose version &>/dev/null; then
        local compose_version
        compose_version=$(docker compose version --short 2>/dev/null || echo "unknown")
        log_success "Docker Compose plugin available: v$compose_version"
    else
        log_error "Docker Compose plugin not available"
        log_info "Installing docker-compose-plugin..."
        apt-get update >> "$LOG_FILE" 2>&1
        apt-get install -y docker-compose-plugin >> "$LOG_FILE" 2>&1
        log_success "Docker Compose plugin installed"
    fi
    
    # Verify Docker works
    log_info "Verifying Docker installation..."
    if docker run --rm hello-world >> "$LOG_FILE" 2>&1; then
        log_success "Docker is working correctly"
    else
        log_warn "Docker verification failed - may need reboot for group permissions"
    fi
}

configure_swap() {
    # Swap increase is only needed for building images locally
    if [[ "${INSTALL_DEPLOY_MODE:-build}" == "release" ]]; then
        log_info "Skipping swap configuration (using pre-built images)"
        return 0
    fi
    
    log_info "Checking swap configuration..."
    
    local current_swap_mb
    current_swap_mb=$(free -m | grep Swap | awk '{print $2}')
    
    if [[ $current_swap_mb -lt 2000 ]]; then
        log_info "Increasing swap to 2GB for image building..."
        
        if [[ -f /etc/dphys-swapfile ]]; then
            # Backup and modify dphys-swapfile config
            cp /etc/dphys-swapfile /etc/dphys-swapfile.backup
            sed -i 's/^CONF_SWAPSIZE=.*/CONF_SWAPSIZE=2048/' /etc/dphys-swapfile
            
            # If CONF_SWAPSIZE doesn't exist, add it
            if ! grep -q "^CONF_SWAPSIZE=" /etc/dphys-swapfile; then
                echo "CONF_SWAPSIZE=2048" >> /etc/dphys-swapfile
            fi
            
            # Restart swap service
            dphys-swapfile swapoff >> "$LOG_FILE" 2>&1 || true
            dphys-swapfile setup >> "$LOG_FILE" 2>&1
            dphys-swapfile swapon >> "$LOG_FILE" 2>&1
            
            log_success "Swap increased to 2GB"
        else
            log_warn "dphys-swapfile not found - skipping swap configuration"
        fi
    else
        log_success "Swap already configured: ${current_swap_mb}MB"
    fi
}

# =============================================================================
# Kiosk Mode Configuration (functions sourced from scripts/kiosk-setup.sh)
# =============================================================================

# Wrapper functions for backward compatibility
install_kiosk_dependencies() {
    kiosk_install_dependencies
}

configure_screen_blanking() {
    kiosk_configure_screen_blanking
}

create_kiosk_script() {
    kiosk_create_startup_script
}

configure_autostart() {
    kiosk_configure_autostart
}

configure_autologin() {
    kiosk_configure_autologin
}

install_ip_update_service() {
    kiosk_install_ip_update_service
}

# =============================================================================
# Application Setup
# =============================================================================

setup_application() {
    log_step "4/6" "Application Setup"
    
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local docker_dir="$script_dir/docker"
    local deploy_mode="${INSTALL_DEPLOY_MODE:-build}"
    
    # Create data directory with permissions that allow docker buildkit to scan
    # (755 allows read access for buildkit, even though folder is in .dockerignore)
    log_info "Creating data directory..."
    mkdir -p "$script_dir/data"
    chmod 755 "$script_dir/data"
    log_success "Data directory created: $script_dir/data"
    
    # Generate secure JWT key
    log_info "Generating secure JWT key..."
    local jwt_key
    jwt_key=$(openssl rand -base64 32)
    
    # Get version
    local version="1.0.0"
    if [[ -f "$script_dir/.version" ]]; then
        version=$(cat "$script_dir/.version")
    fi
    
    # Create .env file
    local env_file="$docker_dir/.env"
    log_info "Creating environment configuration..."
    
    # Get host IP for QR codes
    local host_ip
    host_ip=$(get_ip_address)
    log_info "Detected host IP: $host_ip"
    
    cat > "$env_file" << EOF
# LibraFoto Environment Configuration
# Generated by install.sh on $(date)

# Host IP address (used for QR codes to show correct URL)
LIBRAFOTO_HOST_IP=$host_ip

# JWT signing key (auto-generated, keep secret)
JWT_KEY=$jwt_key

# Application version
VERSION=$version

# Deploy mode: 'build' (local source build) or 'release' (pre-built images from zip)
DEPLOY_MODE=$deploy_mode

EOF

    if [[ "$deploy_mode" == "release" ]]; then
        log_success "Configured for pre-built release images"
    else
        log_success "Configured for local source build"
    fi
}

# =============================================================================
# Container Deployment
# =============================================================================

deploy_containers() {
    log_step "5/6" "Container Deployment"
    
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local docker_dir="$script_dir/docker"
    local deploy_mode="${INSTALL_DEPLOY_MODE:-build}"
    local compose_file
    compose_file=$(get_compose_filename "$script_dir")
    
    cd "$docker_dir"
    
    if [[ "$deploy_mode" == "release" ]]; then
        # Load pre-built images from tar files
        log_info "Loading pre-built container images..."
        echo ""
        
        if ! load_images_from_dir "$script_dir"; then
            log_error "Failed to load images. Check $LOG_FILE for details."
            exit 1
        fi
        
        log_success "Container images loaded successfully"
    else
        # Build images locally from source
        export DOCKER_BUILDKIT=1
        export COMPOSE_DOCKER_CLI_BUILD=1
        
        log_info "Building container images (this may take 10-20 minutes on Pi)..."
        echo ""
        
        if ! docker compose -f "$compose_file" build 2>&1 | tee -a "$LOG_FILE"; then
            log_error "Container build failed. Check $LOG_FILE for details."
            exit 1
        fi
        
        log_success "Container images built successfully"
    fi
    
    # Start containers
    log_info "Starting LibraFoto services..."
    
    if ! docker compose -f "$compose_file" up -d 2>&1 | tee -a "$LOG_FILE"; then
        log_error "Failed to start containers. Check $LOG_FILE for details."
        exit 1
    fi
    
    # Wait for containers to be healthy
    log_info "Waiting for services to be ready..."
    
    local max_wait=120
    local wait_time=0
    local all_healthy=false
    
    while [[ $wait_time -lt $max_wait ]]; do
        local health_status
        health_status=$(docker compose -f "$compose_file" ps --format json 2>/dev/null | grep -o '"Health":"[^"]*"' | sort -u || echo "")
        
        # Check if all containers are running and healthy
        local running_count
        running_count=$(docker compose -f "$compose_file" ps --status running -q 2>/dev/null | wc -l)
        local expected_count
        expected_count=$(docker compose -f "$compose_file" config --services 2>/dev/null | wc -l)
        
        if [[ $running_count -ge $expected_count ]] && [[ $expected_count -gt 0 ]]; then
            # Check health of API container specifically
            local api_health
            api_health=$(docker inspect librafoto-api --format='{{.State.Health.Status}}' 2>/dev/null || echo "unknown")
            
            if [[ "$api_health" == "healthy" ]]; then
                all_healthy=true
                break
            fi
        fi
        
        sleep 5
        wait_time=$((wait_time + 5))
        echo -n "."
    done
    echo ""
    
    if [[ "$all_healthy" == "true" ]]; then
        log_success "All services are running and healthy"
    else
        log_warn "Services started but health check timed out"
        log_info "Checking container status..."
        docker compose -f "$compose_file" ps
    fi
    
    # Show container status
    echo ""
    log_info "Container status:"
    docker compose -f "$compose_file" ps
}

# =============================================================================
# Post-Installation
# =============================================================================

validate_installation() {
    local script_dir="${1:-.}"
    local install_kiosk="${2:-false}"
    local pi_home
    pi_home=$(get_pi_home)
    
    echo -e "\n${BOLD}Validating Installation:${NC}\n"
    
    local validation_passed=true
    
    # Check directories
    if [[ -d "$script_dir/data" ]]; then
        echo -e "  ${GREEN}✓${NC} Data directory created"
    else
        echo -e "  ${RED}✗${NC} Data directory missing"
        validation_passed=false
    fi
    
    # Check .env file
    if [[ -f "$script_dir/docker/.env" ]]; then
        echo -e "  ${GREEN}✓${NC} Environment configuration created"
        
        # Validate required keys
        local env_file="$script_dir/docker/.env"
        for key in LIBRAFOTO_HOST_IP JWT_KEY VERSION DEPLOY_MODE; do
            if grep -q "^${key}=" "$env_file"; then
                echo -e "    ${GREEN}✓${NC} $key configured"
            else
                echo -e "    ${RED}✗${NC} $key missing"
                validation_passed=false
            fi
        done
    else
        echo -e "  ${RED}✗${NC} Environment configuration missing"
        validation_passed=false
    fi
    
    # Check Docker containers
    if check_docker; then
        local compose_file
        compose_file=$(get_compose_filename "$script_dir")
        local running_count
        running_count=$(cd "$script_dir/docker" && docker compose -f "$compose_file" ps --status running -q 2>/dev/null | wc -l)
        
        if [[ $running_count -gt 0 ]]; then
            echo -e "  ${GREEN}✓${NC} Docker containers running ($running_count containers)"
        else
            echo -e "  ${YELLOW}⚠${NC} No containers running (may need time to start)"
        fi
    fi
    
    # Check kiosk files (if kiosk was enabled)
    if [[ "$install_kiosk" == "true" ]]; then
        local kiosk_files_ok=true
        
        if [[ -f "$pi_home/start-kiosk.sh" ]]; then
            echo -e "  ${GREEN}✓${NC} Kiosk startup script created"
        else
            echo -e "  ${RED}✗${NC} Kiosk startup script missing"
            kiosk_files_ok=false
        fi
        
        if systemctl list-unit-files "librafoto-ip-update.service" 2>/dev/null | grep -q librafoto; then
            echo -e "  ${GREEN}✓${NC} IP update service installed"
        else
            echo -e "  ${YELLOW}⚠${NC} IP update service not found"
        fi
        
        if [[ "$kiosk_files_ok" != "true" ]]; then
            validation_passed=false
        fi
    fi
    
    echo ""
    if [[ "$validation_passed" == "true" ]]; then
        log_success "All validation checks passed"
    else
        log_warn "Some validation checks failed - see above"
    fi
    
    return 0
}

show_post_install() {
    log_step "6/6" "Installation Complete"
    
    local ip_address
    ip_address=$(get_ip_address)
    
    local display_url="http://$ip_address/display/"
    local admin_url="http://$ip_address/admin/"
    
    echo ""
    echo -e "${GREEN}${BOLD}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}${BOLD}║          LibraFoto Installation Complete!                  ║${NC}"
    echo -e "${GREEN}${BOLD}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    echo -e "${BOLD}Access URLs:${NC}"
    echo ""
    echo -e "  ${CYAN}Display UI (Slideshow):${NC}"
    echo -e "    $display_url"
    echo ""
    echo -e "  ${CYAN}Admin UI (Management):${NC}"
    echo -e "    $admin_url"
    echo ""
    
    # Generate QR code for Admin UI
    if check_command qrencode; then
        echo -e "${BOLD}Scan to access Admin UI:${NC}"
        echo ""
        qrencode -t ANSIUTF8 -m 2 "$admin_url" 2>/dev/null || true
        echo ""
    fi
    
    echo -e "${BOLD}Next Steps:${NC}"
    echo ""
    echo "  1. ${CYAN}Reboot your Raspberry Pi${NC} to start kiosk mode"
    echo "     Command: sudo reboot"
    echo ""
    echo "  2. ${CYAN}Access the Admin UI${NC} from another device"
    echo "     Create your admin account on first access"
    echo ""
    echo "  3. ${CYAN}Add photos${NC} via local upload or Google Photos"
    echo "     Configure storage providers in Settings"
    echo ""
    echo "  4. ${CYAN}Configure slideshows${NC} and display settings"
    echo ""
    
    local deploy_mode="${INSTALL_DEPLOY_MODE:-build}"
    local compose_file
    compose_file=$(get_compose_filename "$SCRIPT_DIR")
    
    echo -e "${BOLD}Deploy Mode:${NC} $([ "$deploy_mode" == "release" ] && echo "Pre-built images (release zip)" || echo "Local source build")"
    echo ""
    
    echo -e "${BOLD}Useful Commands:${NC}"
    echo ""
    echo "  View logs:     cd docker && docker compose -f $compose_file logs -f"
    echo "  Restart:       cd docker && docker compose -f $compose_file restart"
    echo "  Stop:          cd docker && docker compose -f $compose_file down"
    echo "  Update:        ./update.sh"
    echo ""
    
    echo -e "${BOLD}Log file:${NC} $LOG_FILE"
    echo ""
    
    # Show validation results
    validate_installation "$SCRIPT_DIR" "$install_kiosk"
    
    # Prompt for reboot
    if [[ "$install_kiosk" == "true" ]]; then
        if confirm_prompt "Would you like to reboot now to start kiosk mode?" "Y"; then
            log_info "Rebooting in 5 seconds..."
            sleep 5
            reboot
        else
            log_info "Remember to reboot to start kiosk mode"
        fi
    fi
}

# =============================================================================
# Error Handling
# =============================================================================

cleanup() {
    local exit_code=$?
    
    if [[ $exit_code -ne 0 ]]; then
        echo ""
        log_error "Installation failed with exit code: $exit_code"
        echo ""
        echo "Troubleshooting:"
        echo "  1. Check the log file: $LOG_FILE"
        echo "  2. Ensure internet connectivity"
        echo "  3. Try running with: sudo bash -x $0"
        echo ""
        
        if confirm_prompt "Would you like to view the log file?" "N"; then
            less "$LOG_FILE"
        fi
    fi
}

# =============================================================================
# Main Entry Point
# =============================================================================

main() {
    # Parse arguments - only --help is supported
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --help|-h)
                show_help
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                echo ""
                echo "LibraFoto installation is now fully interactive."
                echo "All options will be presented during the installation process."
                echo ""
                echo "Use --help for more information."
                exit 1
                ;;
        esac
    done
    
    # Initialize
    trap cleanup EXIT
    log_init "LibraFoto Installation"
    
    # Show banner
    show_install_banner
    
    # Check root privileges early (before showing preview)
    check_root
    
    echo ""
    echo -e "${CYAN}This script will guide you through installing LibraFoto on your Raspberry Pi.${NC}"
    echo ""
    echo "First, let's run some system checks..."
    echo ""
    
    # Auto-detect deploy mode based on presence of pre-built images
    local deploy_mode
    if is_release_mode "$SCRIPT_DIR"; then
        deploy_mode="release"
        log_success "Detected release installation (pre-built images found)"
    else
        deploy_mode="build"
        log_success "Detected source installation (will build from repository)"
    fi
    export INSTALL_DEPLOY_MODE="$deploy_mode"
    
    # Run system validation checks
    run_system_checks
    
    # Show preview of what will be installed
    show_install_preview "$SCRIPT_DIR" "$deploy_mode"
    
    # Now ask configuration questions
    echo -e "${BOLD}Configuration Questions:${NC}"
    echo ""
    
    # Question 1: Kiosk mode
    echo -e "${CYAN}1. Kiosk Mode Configuration${NC}"
    echo ""
    echo "Kiosk mode configures this Pi to automatically display the slideshow"
    echo "fullscreen on boot. This is recommended for dedicated photo frames."
    echo ""
    
    local install_kiosk=false
    if confirm_prompt "Would you like to enable kiosk mode?" "Y"; then
        install_kiosk=true
        log_success "Kiosk mode will be installed"
    else
        log_info "Kiosk mode will not be installed"
        log_info "You can enable it later with: sudo bash scripts/kiosk-setup.sh"
    fi
    
    # Question 2: Final confirmation
    echo ""
    echo -e "${CYAN}2. Confirmation${NC}"
    echo ""
    echo "Ready to install with:"
    echo "  • Deploy mode: $([ "$deploy_mode" == "release" ] && echo "Pre-built images" || echo "Build from source")"
    echo "  • Kiosk mode: $([ "$install_kiosk" == "true" ] && echo "Enabled" || echo "Disabled")"
    echo ""
    
    if ! confirm_prompt "Proceed with installation?" "Y"; then
        echo "Installation cancelled."
        exit 0
    fi
    
    echo ""
    
    # Run installation steps
    install_docker
    configure_swap
    
    if [[ "$install_kiosk" == "true" ]]; then
        kiosk_full_setup "3/6"
    else
        log_info "Skipping kiosk configuration"
    fi
    
    setup_application
    deploy_containers
    show_post_install
}

# Run main function
main "$@"

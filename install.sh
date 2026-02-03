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
#   sudo ./install.sh           # Full installation
#   sudo ./install.sh --help    # Show help
#   sudo ./install.sh --uninstall  # Remove LibraFoto
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
    --help          Show this help message
    --uninstall     Remove LibraFoto (keeps photos data)
    --skip-kiosk    Skip kiosk mode configuration
    --skip-docker   Skip Docker installation (if already installed)
    --repair-kiosk  Repair/reconfigure kiosk mode only

DESCRIPTION:
    This script automates the complete setup of LibraFoto on a Raspberry Pi,
    including Docker installation, kiosk mode configuration, and container
    deployment.

REQUIREMENTS:
    - Raspberry Pi 4 or later
    - Raspberry Pi OS 64-bit with Desktop (Bookworm or later)
    - Internet connection
    - At least 2GB RAM
    - Run from the cloned LibraFoto repository directory

EXAMPLES:
    sudo ./install.sh              # Full installation
    sudo ./install.sh --skip-kiosk # Install without kiosk mode
    sudo ./install.sh --repair-kiosk # Fix kiosk mode if not working

LOG FILE:
    Installation log is saved to: $LOG_FILE

EOF
}

show_overview() {
    echo -e "${BOLD}Installation Overview:${NC}"
    echo ""
    echo "  1. System Validation"
    echo "     - Verify Raspberry Pi 4+ with 64-bit OS"
    echo "     - Check available RAM and storage"
    echo ""
    echo "  2. Docker Setup"
    echo "     - Install Docker and Docker Compose"
    echo "     - Configure for non-root access"
    echo ""
    echo "  3. Kiosk Mode Configuration"
    echo "     - Install Chromium browser"
    echo "     - Configure auto-start on boot"
    echo "     - Disable screen blanking"
    echo ""
    echo "  4. Application Deployment"
    echo "     - Build container images"
    echo "     - Start LibraFoto services"
    echo ""
    echo "  5. Post-Installation"
    echo "     - Display access URLs and QR code"
    echo "     - Show next steps"
    echo ""
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
    
    log_success "Repository files verified"
    return 0
}

check_internet_connectivity() {
    log_info "Checking internet connectivity..."
    
    if ! check_internet; then
        log_error "No internet connection detected"
        log_info "Internet is required for Docker installation and image downloads"
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

EOF

}

# =============================================================================
# Container Deployment
# =============================================================================

deploy_containers() {
    log_step "5/6" "Container Deployment"
    
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local docker_dir="$script_dir/docker"
    
    cd "$docker_dir"
    
    # Enable BuildKit
    export DOCKER_BUILDKIT=1
    export COMPOSE_DOCKER_CLI_BUILD=1
    
    # Build containers
    log_info "Building container images (this may take 10-20 minutes on Pi)..."
    echo ""
    
    if ! docker compose build 2>&1 | tee -a "$LOG_FILE"; then
        log_error "Container build failed. Check $LOG_FILE for details."
        exit 1
    fi
    
    log_success "Container images built successfully"
    
    # Start containers
    log_info "Starting LibraFoto services..."
    
    if ! docker compose up -d 2>&1 | tee -a "$LOG_FILE"; then
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
        health_status=$(docker compose ps --format json 2>/dev/null | grep -o '"Health":"[^"]*"' | sort -u || echo "")
        
        # Check if all containers are running and healthy
        local running_count
        running_count=$(docker compose ps --status running -q 2>/dev/null | wc -l)
        local expected_count
        expected_count=$(docker compose config --services 2>/dev/null | wc -l)
        
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
        docker compose ps
    fi
    
    # Show container status
    echo ""
    log_info "Container status:"
    docker compose ps
}

# =============================================================================
# Post-Installation
# =============================================================================

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
    
    echo -e "${BOLD}Useful Commands:${NC}"
    echo ""
    echo "  View logs:     cd docker && docker compose logs -f"
    echo "  Restart:       cd docker && docker compose restart"
    echo "  Stop:          cd docker && docker compose down"
    echo "  Update:        git pull && cd docker && docker compose pull && docker compose up -d --build"
    echo ""
    
    echo -e "${BOLD}Log file:${NC} $LOG_FILE"
    echo ""
    
    # Prompt for reboot
    if confirm_prompt "Would you like to reboot now to start kiosk mode?" "Y"; then
        log_info "Rebooting in 5 seconds..."
        sleep 5
        reboot
    else
        log_info "Remember to reboot to start kiosk mode"
    fi
}

# =============================================================================
# Uninstall
# =============================================================================

uninstall() {
    show_install_banner
    
    echo -e "${YELLOW}${BOLD}LibraFoto Uninstallation${NC}"
    echo ""
    echo "This will remove:"
    echo "  - Docker containers and images"
    echo "  - Kiosk mode configuration"
    echo ""
    echo -e "${YELLOW}This will NOT remove:${NC}"
    echo "  - Your photos and database (data/ directory)"
    echo "  - Docker itself"
    echo ""
    
    if ! confirm_prompt "Are you sure you want to uninstall LibraFoto?" "N"; then
        echo "Uninstallation cancelled."
        exit 0
    fi
    
    local script_dir
    script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    local docker_dir="$script_dir/docker"
    local pi_home
    pi_home=$(get_pi_home)
    
    log_info "Stopping containers..."
    if [[ -d "$docker_dir" ]]; then
        cd "$docker_dir"
        docker compose down >> "$LOG_FILE" 2>&1 || true
    fi
    
    if confirm_prompt "Remove Docker volumes (this will delete your photos and database)?" "N"; then
        docker compose down -v >> "$LOG_FILE" 2>&1 || true
        log_warn "Docker volumes removed - data deleted"
    else
        log_info "Docker volumes preserved"
    fi
    
    log_info "Removing kiosk configuration..."
    
    # Use kiosk uninstall function if available
    if declare -f kiosk_uninstall &>/dev/null; then
        kiosk_uninstall
    else
        # Fallback to inline removal
        rm -f "$pi_home/start-kiosk.sh"
        rm -f "$pi_home/.config/autostart/librafoto-kiosk.desktop"
        
        local lxde_autostart="/etc/xdg/lxsession/LXDE-pi/autostart"
        if [[ -f "$lxde_autostart" ]]; then
            sed -i '/start-kiosk.sh/d' "$lxde_autostart"
            sed -i '/LibraFoto/d' "$lxde_autostart"
        fi
        
        if [[ -f "/etc/lightdm/lightdm.conf.backup" ]]; then
            mv /etc/lightdm/lightdm.conf.backup /etc/lightdm/lightdm.conf
        fi
        
        log_success "Kiosk configuration removed"
    fi
    
    # Remove .env file
    rm -f "$docker_dir/.env"
    
    echo ""
    log_success "LibraFoto uninstalled successfully"
    echo ""
    echo "Your data directory is preserved at: $script_dir/data"
    echo "To completely remove, manually delete the LibraFoto directory."
    echo ""
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
    # Parse arguments
    local skip_kiosk=false
    local skip_docker=false
    local repair_kiosk=false
    
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --help|-h)
                show_help
                exit 0
                ;;
            --uninstall)
                check_root
                uninstall
                exit 0
                ;;
            --skip-kiosk)
                skip_kiosk=true
                shift
                ;;
            --skip-docker)
                skip_docker=true
                shift
                ;;
            --repair-kiosk)
                repair_kiosk=true
                shift
                ;;
            *)
                log_error "Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done
    
    # Initialize
    trap cleanup EXIT
    log_init "LibraFoto Installation"
    
    # Handle kiosk repair mode
    if [[ "$repair_kiosk" == "true" ]]; then
        show_install_banner
        check_root
        
        if [[ -f "$KIOSK_SCRIPT" ]]; then
            kiosk_full_setup "1/1"
            log_success "Kiosk mode repaired. Reboot to apply changes."
            if confirm_prompt "Would you like to reboot now?" "Y"; then
                log_info "Rebooting in 5 seconds..."
                sleep 5
                reboot
            fi
        else
            log_error "Kiosk setup script not found: $KIOSK_SCRIPT"
            exit 1
        fi
        exit 0
    fi
    
    # Show banner and overview
    show_install_banner
    show_overview
    
    # Confirm installation
    if ! confirm_prompt "Do you want to proceed with the installation?" "Y"; then
        echo "Installation cancelled."
        exit 0
    fi
    
    echo ""
    
    # Ask about kiosk mode before starting (unless --skip-kiosk was passed)
    local install_kiosk=false
    if [[ "$skip_kiosk" != "true" ]]; then
        echo ""
        echo -e "${BOLD}Kiosk Mode Configuration${NC}"
        echo ""
        echo "Kiosk mode configures this Pi to automatically display the slideshow"
        echo "fullscreen on boot. This is recommended for dedicated photo frames."
        echo ""
        if confirm_prompt "Would you like to enable kiosk mode?" "Y"; then
            install_kiosk=true
        else
            log_info "Kiosk mode will not be installed"
            log_info "You can enable it later with: sudo bash scripts/kiosk-setup.sh"
        fi
    fi
    
    echo ""
    
    # Check root privileges
    check_root
    
    # Run installation steps
    run_system_checks
    
    if [[ "$skip_docker" != "true" ]]; then
        install_docker
        configure_swap
    else
        log_info "Skipping Docker installation (--skip-docker)"
    fi
    
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

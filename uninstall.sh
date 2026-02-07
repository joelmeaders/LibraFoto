#!/bin/bash
#
# LibraFoto - Uninstall Script
#
# This script removes LibraFoto from a Raspberry Pi:
# - Stops and removes Docker containers
# - Optionally removes Docker images and volumes
# - Removes kiosk mode configuration
# - Cleans up systemd services
# - Preserves data by default (photos, database)
#
# Usage:
#   sudo ./uninstall.sh           # Interactive uninstall
#   sudo ./uninstall.sh --help    # Show help
#   sudo ./uninstall.sh --force   # Uninstall without confirmation
#   sudo ./uninstall.sh --purge   # Remove everything including data
#
# Version: 1.0.0
# Repository: https://github.com/librafoto/librafoto

set -euo pipefail

# =============================================================================
# Configuration
# =============================================================================

readonly SCRIPT_VERSION="1.0.0"
readonly LOG_FILE="/tmp/librafoto-uninstall.log"

# Cache the script directory at startup
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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

# =============================================================================
# Banner and Help
# =============================================================================

# Uninstall-specific banner
show_uninstall_banner() {
    show_banner "Uninstall Script"
}

show_help() {
    show_uninstall_banner
    cat << EOF
Usage: sudo $0 [OPTIONS]

OPTIONS:
    --help, -h      Show this help message
    --force, -f     Uninstall without confirmation prompts
    --purge         Remove everything including data (photos, database)
    --keep-docker   Keep Docker images (only remove containers)
    --dry-run       Show what would be removed without removing

DESCRIPTION:
    This script removes LibraFoto from a Raspberry Pi including:
    - Docker containers and networks
    - Optionally Docker images and volumes
    - Kiosk mode configuration
    - Autostart entries
    - Systemd services

WHAT IS PRESERVED BY DEFAULT:
    - Your photos and database (data/ directory)
    - Docker installation itself
    - Backups directory

EXAMPLES:
    sudo ./uninstall.sh              # Interactive uninstall
    sudo ./uninstall.sh --force      # Non-interactive uninstall
    sudo ./uninstall.sh --purge      # Remove everything including data
    sudo ./uninstall.sh --dry-run    # Preview what would be removed

LOG FILE:
    Uninstall log is saved to: $LOG_FILE

EOF
}

# =============================================================================
# Docker Container Removal
# =============================================================================

stop_containers() {
    log_step "1/5" "Stopping Containers"
    
    local docker_dir="$SCRIPT_DIR/docker"
    
    if [[ ! -d "$docker_dir" ]]; then
        log_warn "Docker directory not found at $docker_dir"
        return 0
    fi
    
    if ! check_docker; then
        return 0
    fi
    
    cd "$docker_dir"
    
    local compose_file
    compose_file=$(get_compose_filename "$SCRIPT_DIR")
    
    log_info "Stopping LibraFoto containers..."
    
    if docker compose -f "$compose_file" ps -q 2>/dev/null | grep -q .; then
        docker compose -f "$compose_file" stop >> "$LOG_FILE" 2>&1 || true
        log_success "Containers stopped"
    else
        log_info "No running containers found"
    fi
}

remove_containers() {
    local keep_volumes="$1"
    
    log_step "2/5" "Removing Containers"
    
    local docker_dir="$SCRIPT_DIR/docker"
    
    if [[ ! -d "$docker_dir" ]]; then
        return 0
    fi
    
    if ! check_docker; then
        return 0
    fi
    
    cd "$docker_dir"
    
    local compose_file
    compose_file=$(get_compose_filename "$SCRIPT_DIR")
    
    log_info "Removing containers and networks..."
    
    if [[ "$keep_volumes" == "true" ]]; then
        docker compose -f "$compose_file" down >> "$LOG_FILE" 2>&1 || true
        log_success "Containers and networks removed (volumes preserved)"
    else
        docker compose -f "$compose_file" down -v >> "$LOG_FILE" 2>&1 || true
        log_success "Containers, networks, and volumes removed"
    fi
}

remove_images() {
    log_step "3/5" "Removing Docker Images"
    
    if ! check_docker; then
        return 0
    fi
    
    log_info "Finding LibraFoto Docker images..."
    
    # Find and remove LibraFoto images
    local images
    images=$(docker images --format "{{.Repository}}:{{.Tag}} {{.ID}}" | grep -E "librafoto|docker-" | awk '{print $2}' || true)
    
    if [[ -n "$images" ]]; then
        echo "$images" | while read -r image_id; do
            if [[ -n "$image_id" ]]; then
                log_info "Removing image: $image_id"
                docker rmi "$image_id" >> "$LOG_FILE" 2>&1 || true
            fi
        done
        log_success "Docker images removed"
    else
        log_info "No LibraFoto Docker images found"
    fi
    
    # Prune dangling images
    log_info "Pruning dangling images..."
    docker image prune -f >> "$LOG_FILE" 2>&1 || true
}

# =============================================================================
# Kiosk Mode Removal
# =============================================================================

remove_kiosk() {
    log_step "4/5" "Removing Kiosk Configuration"
    
    local pi_home
    pi_home=$(get_pi_home)
    local pi_user
    pi_user=$(get_pi_user)
    
    # Remove kiosk startup script
    if [[ -f "$pi_home/start-kiosk.sh" ]]; then
        rm -f "$pi_home/start-kiosk.sh"
        log_info "Removed kiosk startup script"
    fi
    
    # Remove XDG autostart entry
    if [[ -f "$pi_home/.config/autostart/librafoto-kiosk.desktop" ]]; then
        rm -f "$pi_home/.config/autostart/librafoto-kiosk.desktop"
        log_info "Removed XDG autostart entry"
    fi
    
    # Remove from LXDE autostart
    local lxde_autostart="/etc/xdg/lxsession/LXDE-pi/autostart"
    if [[ -f "$lxde_autostart" ]]; then
        if grep -q "start-kiosk.sh\|LibraFoto" "$lxde_autostart" 2>/dev/null; then
            sed -i '/start-kiosk.sh/d' "$lxde_autostart"
            sed -i '/LibraFoto/d' "$lxde_autostart"
            log_info "Removed from LXDE autostart"
        fi
    fi
    
    # Remove user LXDE autostart
    local user_lxde_autostart="$pi_home/.config/lxsession/LXDE-pi/autostart"
    if [[ -f "$user_lxde_autostart" ]]; then
        if grep -q "start-kiosk.sh\|LibraFoto" "$user_lxde_autostart" 2>/dev/null; then
            sed -i '/start-kiosk.sh/d' "$user_lxde_autostart"
            sed -i '/LibraFoto/d' "$user_lxde_autostart"
            log_info "Removed from user LXDE autostart"
        fi
    fi
    
    # Restore lightdm config if backup exists
    if [[ -f "/etc/lightdm/lightdm.conf.backup" ]]; then
        mv /etc/lightdm/lightdm.conf.backup /etc/lightdm/lightdm.conf
        log_info "Restored LightDM configuration"
    fi
    
    # Restore LXDE autostart if backup exists
    if [[ -f "${lxde_autostart}.backup" ]]; then
        mv "${lxde_autostart}.backup" "$lxde_autostart"
        log_info "Restored LXDE autostart configuration"
    fi
    
    log_success "Kiosk configuration removed"
}

# =============================================================================
# Systemd Services Removal
# =============================================================================

remove_services() {
    log_step "5/5" "Removing Systemd Services"
    
    local services_removed=0
    
    # Remove IP update service
    if [[ -f "/etc/systemd/system/librafoto-ip-update.service" ]]; then
        log_info "Removing IP update service..."
        systemctl stop librafoto-ip-update.service >> "$LOG_FILE" 2>&1 || true
        systemctl disable librafoto-ip-update.service >> "$LOG_FILE" 2>&1 || true
        rm -f /etc/systemd/system/librafoto-ip-update.service
        services_removed=$((services_removed + 1))
    fi
    
    # Remove any other LibraFoto services
    for service in /etc/systemd/system/librafoto*.service; do
        if [[ -f "$service" ]]; then
            local service_name
            service_name=$(basename "$service")
            log_info "Removing $service_name..."
            systemctl stop "$service_name" >> "$LOG_FILE" 2>&1 || true
            systemctl disable "$service_name" >> "$LOG_FILE" 2>&1 || true
            rm -f "$service"
            services_removed=$((services_removed + 1))
        fi
    done
    
    if [[ $services_removed -gt 0 ]]; then
        systemctl daemon-reload >> "$LOG_FILE" 2>&1
        log_success "Removed $services_removed systemd service(s)"
    else
        log_info "No LibraFoto systemd services found"
    fi
}

# =============================================================================
# Configuration Cleanup
# =============================================================================

remove_config() {
    local docker_dir="$SCRIPT_DIR/docker"
    
    # Remove .env file
    if [[ -f "$docker_dir/.env" ]]; then
        rm -f "$docker_dir/.env"
        log_info "Removed environment configuration"
    fi
}

# =============================================================================
# Data Removal (Optional)
# =============================================================================

remove_data() {
    local data_dir="$SCRIPT_DIR/data"
    local backup_dir="$SCRIPT_DIR/backups"
    
    log_warn "This will permanently delete all photos and database!"
    echo ""
    
    if [[ -d "$data_dir" ]]; then
        log_info "Data directory size:"
        du -sh "$data_dir" 2>/dev/null || echo "  (unable to determine size)"
        echo ""
        
        rm -rf "$data_dir"
        log_success "Data directory removed: $data_dir"
    else
        log_info "Data directory not found"
    fi
    
    if [[ -d "$backup_dir" ]]; then
        log_info "Backup directory size:"
        du -sh "$backup_dir" 2>/dev/null || echo "  (unable to determine size)"
        
        rm -rf "$backup_dir"
        log_success "Backup directory removed: $backup_dir"
    fi
}

# =============================================================================
# Dry Run
# =============================================================================

show_dry_run() {
    show_uninstall_banner
    
    echo -e "${BOLD}Dry Run - The following would be removed:${NC}"
    echo ""
    
    local docker_dir="$SCRIPT_DIR/docker"
    local pi_home
    pi_home=$(get_pi_home)
    
    echo -e "${CYAN}Docker Resources:${NC}"
    if check_docker && [[ -d "$docker_dir" ]]; then
        cd "$docker_dir"
        local compose_file
        compose_file=$(get_compose_filename "$SCRIPT_DIR")
        echo "  Containers:"
        docker compose -f "$compose_file" ps --format "    - {{.Name}}" 2>/dev/null || echo "    (none found)"
        echo ""
        echo "  Images:"
        docker images --format "    - {{.Repository}}:{{.Tag}}" | grep -E "librafoto|docker-" || echo "    (none found)"
        echo ""
        echo "  Volumes:"
        docker volume ls --format "    - {{.Name}}" | grep -E "librafoto|docker_" || echo "    (none found)"
    else
        echo "  (Docker not available)"
    fi
    echo ""
    
    echo -e "${CYAN}Kiosk Configuration:${NC}"
    [[ -f "$pi_home/start-kiosk.sh" ]] && echo "  - $pi_home/start-kiosk.sh"
    [[ -f "$pi_home/.config/autostart/librafoto-kiosk.desktop" ]] && echo "  - $pi_home/.config/autostart/librafoto-kiosk.desktop"
    [[ -f "/etc/xdg/lxsession/LXDE-pi/autostart" ]] && grep -q "LibraFoto\|start-kiosk" /etc/xdg/lxsession/LXDE-pi/autostart 2>/dev/null && echo "  - LXDE autostart entries"
    echo ""
    
    echo -e "${CYAN}Systemd Services:${NC}"
    ls /etc/systemd/system/librafoto*.service 2>/dev/null | sed 's/^/  - /' || echo "  (none found)"
    echo ""
    
    echo -e "${CYAN}Configuration Files:${NC}"
    [[ -f "$docker_dir/.env" ]] && echo "  - $docker_dir/.env"
    echo ""
    
    echo -e "${YELLOW}Preserved (unless --purge is used):${NC}"
    [[ -d "$SCRIPT_DIR/data" ]] && echo "  - $SCRIPT_DIR/data ($(du -sh "$SCRIPT_DIR/data" 2>/dev/null | cut -f1))"
    [[ -d "$SCRIPT_DIR/backups" ]] && echo "  - $SCRIPT_DIR/backups"
    echo ""
}

# =============================================================================
# Post-Uninstall
# =============================================================================

show_post_uninstall() {
    local purge_mode="$1"
    
    echo ""
    echo -e "${GREEN}${BOLD}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}${BOLD}║           LibraFoto Uninstallation Complete!               ║${NC}"
    echo -e "${GREEN}${BOLD}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    if [[ "$purge_mode" != "true" ]]; then
        echo -e "${BOLD}Preserved Data:${NC}"
        echo ""
        if [[ -d "$SCRIPT_DIR/data" ]]; then
            echo "  Your photos and database are preserved at:"
            echo "    $SCRIPT_DIR/data"
            echo ""
        fi
        if [[ -d "$SCRIPT_DIR/backups" ]]; then
            echo "  Backups are preserved at:"
            echo "    $SCRIPT_DIR/backups"
            echo ""
        fi
    fi
    
    echo -e "${BOLD}To completely remove LibraFoto:${NC}"
    echo ""
    echo "  1. Delete the LibraFoto directory (use -rf for .git files):"
    echo "     sudo rm -rf $SCRIPT_DIR"
    echo ""
    echo "  2. Optionally remove Docker (if no longer needed):"
    echo "     sudo apt remove docker-ce docker-ce-cli containerd.io"
    echo ""
    
    echo -e "${BOLD}To reinstall LibraFoto:${NC}"
    echo ""
    echo "  sudo ./install.sh"
    echo ""
    
    echo -e "${BOLD}Log file:${NC} $LOG_FILE"
    echo ""
}

# =============================================================================
# Main Uninstall Logic
# =============================================================================

perform_uninstall() {
    local force_mode="$1"
    local purge_mode="$2"
    local keep_images="$3"
    
    # Show what will be removed
    echo -e "${YELLOW}${BOLD}LibraFoto Uninstallation${NC}"
    echo ""
    echo "This will remove:"
    echo "  - Docker containers and networks"
    if [[ "$keep_images" != "true" ]]; then
        echo "  - Docker images"
    fi
    echo "  - Kiosk mode configuration"
    echo "  - Systemd services"
    echo "  - Environment configuration"
    echo ""
    
    if [[ "$purge_mode" == "true" ]]; then
        echo -e "${RED}${BOLD}PURGE MODE: This will also delete:${NC}"
        echo "  - All photos"
        echo "  - Database"
        echo "  - Backups"
        echo ""
    else
        echo -e "${YELLOW}This will NOT remove:${NC}"
        echo "  - Your photos and database (data/ directory)"
        echo "  - Docker itself"
        echo "  - Backups directory"
        echo ""
    fi
    
    if [[ "$force_mode" != "true" ]]; then
        if ! confirm_prompt "Are you sure you want to uninstall LibraFoto?" "N"; then
            echo ""
            log_info "Uninstallation cancelled"
            exit 0
        fi
        echo ""
    fi
    
    # Stop containers
    stop_containers
    
    # Remove containers (ask about volumes if not in purge mode)
    local keep_volumes="true"
    if [[ "$purge_mode" == "true" ]]; then
        keep_volumes="false"
    elif [[ "$force_mode" != "true" ]]; then
        echo ""
        if confirm_prompt "Remove Docker volumes (this will delete database data)?" "N"; then
            keep_volumes="false"
        fi
    fi
    remove_containers "$keep_volumes"
    
    # Remove images unless skipped
    if [[ "$keep_images" != "true" ]]; then
        remove_images
    else
        log_info "Skipping Docker image removal (--keep-docker)"
    fi
    
    # Remove kiosk configuration
    remove_kiosk
    
    # Remove systemd services
    remove_services
    
    # Remove configuration
    remove_config
    
    # Remove data if purge mode
    if [[ "$purge_mode" == "true" ]]; then
        echo ""
        if [[ "$force_mode" != "true" ]]; then
            if confirm_prompt "FINAL WARNING: Delete all photos, database, and backups?" "N"; then
                remove_data
            else
                log_info "Data preserved"
            fi
        else
            remove_data
        fi
    fi
    
    # Show completion message
    show_post_uninstall "$purge_mode"
}

# =============================================================================
# Main Entry Point
# =============================================================================

main() {
    local force_mode=false
    local purge_mode=false
    local keep_images=false
    local dry_run=false
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --help|-h)
                show_help
                exit 0
                ;;
            --force|-f)
                force_mode=true
                shift
                ;;
            --purge)
                purge_mode=true
                shift
                ;;
            --keep-docker)
                keep_images=true
                shift
                ;;
            --dry-run)
                dry_run=true
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
    show_uninstall_banner
    log_init "LibraFoto Uninstall"
    
    # Check root
    check_root
    
    # Dry run mode
    if [[ "$dry_run" == "true" ]]; then
        show_dry_run
        exit 0
    fi
    
    # Perform uninstall
    perform_uninstall "$force_mode" "$purge_mode" "$keep_images"
    
    log_success "Uninstallation complete"
}

# Run main function
main "$@"

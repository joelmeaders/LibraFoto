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

DESCRIPTION:
    This script is fully interactive and will guide you through uninstalling
    LibraFoto from your Raspberry Pi.
    
    During the uninstall process, you will be asked to confirm:
    - Uninstallation (yes/no)
    - Removal of Docker images (yes/no)
    - Removal of Docker volumes/database (yes/no)
    - Removal of data directories including photos and backups (yes/no)
    
    The script will:
    - Stop and remove Docker containers
    - Remove kiosk mode configuration
    - Clean up systemd services
    - Track all operations and show a summary
    - Validate that resources were properly removed

EXAMPLE:
    sudo ./uninstall.sh

LOG FILE:
    Uninstall log is saved to: $LOG_FILE

EOF
}

# =============================================================================
# Docker Container Removal
# =============================================================================

stop_containers() {
    log_info "Stopping LibraFoto containers..."
    
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
    
    if docker compose -f "$compose_file" ps -q 2>/dev/null | grep -q .; then
        docker compose -f "$compose_file" stop >> "$LOG_FILE" 2>&1 || true
        log_success "Containers stopped"
    else
        log_info "No running containers found"
    fi
}

remove_containers() {
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
    docker compose -f "$compose_file" down >> "$LOG_FILE" 2>&1 || true
    log_success "Containers and networks removed (volumes preserved)"
}

remove_volumes() {
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
    
    log_info "Removing Docker volumes..."
    docker compose -f "$compose_file" down -v >> "$LOG_FILE" 2>&1 || true
    log_success "Volumes removed"
}

remove_images() {
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
    local removed_data="$1"
    
    echo ""
    echo -e "${GREEN}${BOLD}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}${BOLD}║           LibraFoto Uninstallation Complete!               ║${NC}"
    echo -e "${GREEN}${BOLD}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    if [[ "$removed_data" != "true" ]]; then
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

show_post_uninstall_with_validation() {
    local removed_images="$1"
    local removed_volumes="$2"
    local removed_data="$3"
    
    # Show standard post-uninstall message
    show_post_uninstall "$removed_data"
    
    # Show operation summary
    show_operation_summary
    
    # Validate resource removal
    echo -e "\n${BOLD}Validating removal...${NC}\n"
    
    local validation_targets=("containers" "kiosk" "config")
    [[ "$removed_images" == "true" ]] && validation_targets+=("images")
    [[ "$removed_volumes" == "true" ]] && validation_targets+=("volumes")
    [[ "$removed_data" == "true" ]] && validation_targets+=("data")
    
    if validate_removal "${validation_targets[@]}"; then
        log_success "All resources validated as removed"
    else
        log_warn "Some resources may require manual cleanup"
    fi
}

# =============================================================================
# Main Uninstall Logic
# =============================================================================

perform_uninstall() {
    local remove_images="$1"
    local remove_volumes="$2"
    local remove_data="$3"
    
    echo ""
    echo -e "${CYAN}${BOLD}Starting uninstall operations...${NC}"
    echo ""
    
    # Enable non-fatal errors for tracked operations
    set +e
    
    # Step 1: Stop containers (always if Docker available)
    log_step "1/8" "Stopping Containers"
    track_operation "Stop containers" stop_containers || true
    
    # Step 2: Remove containers (always)
    log_step "2/8" "Removing Containers"
    track_operation "Remove containers" remove_containers || true
    
    # Step 3: Remove volumes (if user chose yes)
    if [[ "$remove_volumes" == "true" ]]; then
        log_step "3/8" "Removing Docker Volumes"
        track_operation "Remove volumes" remove_volumes || true
    else
        log_step "3/8" "Skipping Volume Removal"
        log_info "Docker volumes preserved"
    fi
    
    # Step 4: Remove images (if user chose yes)
    if [[ "$remove_images" == "true" ]]; then
        log_step "4/8" "Removing Docker Images"
        track_operation "Remove images" remove_images || true
    else
        log_step "4/8" "Skipping Image Removal"
        log_info "Docker images preserved"
    fi
    
    # Step 5: Remove kiosk (always)
    log_step "5/8" "Removing Kiosk Configuration"
    track_operation "Remove kiosk" remove_kiosk || true
    
    # Step 6: Remove services (always)
    log_step "6/8" "Removing Systemd Services"
    track_operation "Remove services" remove_services || true
    
    # Step 7: Remove config (always)
    log_step "7/8" "Removing Configuration"
    track_operation "Remove config" remove_config || true
    
    # Step 8: Remove data (if user chose yes)
    if [[ "$remove_data" == "true" ]]; then
        log_step "8/8" "Removing Data Directories"
        log_warn "Removing all photos, database, and backups..."
        track_operation "Remove data" remove_data || true
    else
        log_step "8/8" "Skipping Data Removal"
        log_info "Data directories preserved"
    fi
    
    # Re-enable fatal errors
    set -e
    
    # Show completion with validation
    show_post_uninstall_with_validation "$remove_images" "$remove_volumes" "$remove_data"
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
                echo "LibraFoto uninstall is now fully interactive."
                echo "All options will be presented during the uninstall process."
                echo ""
                echo "Use --help for more information."
                exit 1
                ;;
        esac
    done
    
    # Initialize
    show_uninstall_banner
    log_init "LibraFoto Uninstall"
    reset_operation_tracking
    
    # Check root privileges
    check_root
    
    echo ""
    echo -e "${CYAN}This script will guide you through uninstalling LibraFoto.${NC}"
    echo ""
    echo "First, let's preview what will be removed..."
    echo ""
    
    # Show preview of what will be removed
    show_dry_run
    
    # Ask configuration questions
    echo -e "${BOLD}Configuration Questions:${NC}"
    echo ""
    
    # Question 1: Confirm uninstallation
    echo -e "${CYAN}1. Uninstallation Confirmation${NC}"
    echo ""
    if ! confirm_prompt "Do you want to proceed with uninstalling LibraFoto?" "N"; then
        echo ""
        log_info "Uninstallation cancelled by user"
        exit 0
    fi
    echo ""
    
    # Question 2: Remove Docker images
    echo -e "${CYAN}2. Docker Images${NC}"
    echo ""
    echo "  Docker images contain the LibraFoto application code."
    echo "  Removing them will free up disk space (~500MB)."
    echo "  They will be rebuilt or reloaded if you reinstall."
    echo ""
    local remove_images=false
    if confirm_prompt "Remove Docker images?" "N"; then
        remove_images=true
    fi
    echo ""
    
    # Question 3: Remove Docker volumes
    echo -e "${CYAN}3. Docker Volumes (Database)${NC}"
    echo ""
    echo "  ${YELLOW}WARNING:${NC} Docker volumes contain your LibraFoto database."
    echo "  This includes album organization, tags, and photo metadata."
    echo "  Your actual photo files will NOT be removed (see next question)."
    echo ""
    local remove_volumes=false
    if confirm_prompt "Remove Docker volumes (deletes database)?" "N"; then
        remove_volumes=true
    fi
    echo ""
    
    # Question 4: Remove data directories
    echo -e "${CYAN}4. Data Directories (Photos & Backups)${NC}"
    echo ""
    echo "  ${RED}${BOLD}DANGER:${NC} This will ${RED}permanently delete${NC} all your photos and backups!"
    echo ""
    if [[ -d "$SCRIPT_DIR/data" ]]; then
        echo "  Data directory size: $(du -sh "$SCRIPT_DIR/data" 2>/dev/null | cut -f1)"
    fi
    if [[ -d "$SCRIPT_DIR/backups" ]]; then
        echo "  Backups directory size: $(du -sh "$SCRIPT_DIR/backups" 2>/dev/null | cut -f1)"
    fi
    echo ""
    local remove_data=false
    if confirm_prompt "Remove data directories (photos & backups)?" "N"; then
        echo ""
        # Double confirmation for data removal
        echo -e "  ${RED}${BOLD}FINAL WARNING:${NC} Are you absolutely sure?"
        echo "  This action ${RED}CANNOT BE UNDONE${NC}!"
        echo ""
        if confirm_prompt "  Type YES to confirm data deletion" "N"; then
            remove_data=true
        else
            log_info "Data directories will be preserved"
        fi
    fi
    echo ""
    
    # Perform uninstall with error tracking
    perform_uninstall "$remove_images" "$remove_volumes" "$remove_data"
    
    log_success "Uninstallation script complete"
}

# Run main function
main "$@"

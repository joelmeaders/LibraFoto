#!/bin/bash
#
# LibraFoto - Kiosk Mode Setup Script
#
# This script configures Raspberry Pi kiosk mode for LibraFoto:
# - Installs Chromium and kiosk dependencies
# - Configures screen blanking prevention
# - Sets up autostart for fullscreen display
# - Configures desktop autologin
#
# Usage:
#   sudo bash kiosk-setup.sh              # Install kiosk mode
#   sudo bash kiosk-setup.sh --uninstall  # Remove kiosk mode
#   sudo bash kiosk-setup.sh --status     # Check kiosk status
#
# This script can be run standalone or sourced by install.sh/update.sh
#
# Version: 1.0.0
# Repository: https://github.com/librafoto/librafoto

# Only set these if not already defined (allows sourcing by parent script)
if [[ -z "${KIOSK_SCRIPT_SOURCED:-}" ]]; then
    set -euo pipefail
    KIOSK_SCRIPT_SOURCED=true
fi

# =============================================================================
# Configuration
# =============================================================================

readonly KIOSK_SCRIPT_VERSION="1.0.0"
readonly KIOSK_LOG_FILE="${LOG_FILE:-/tmp/librafoto-kiosk.log}"
readonly KIOSK_DISPLAY_URL="${DISPLAY_URL:-http://localhost/display/}"

# =============================================================================
# Source Common Helpers (if not already sourced by parent)
# =============================================================================

if [[ -z "${LIBRAFOTO_COMMON_SOURCED:-}" ]]; then
    # Find and source common.sh
    KIOSK_SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    if [[ -f "$KIOSK_SCRIPT_DIR/common.sh" ]]; then
        LOG_FILE="$KIOSK_LOG_FILE"
        source "$KIOSK_SCRIPT_DIR/common.sh"
    fi
fi

# =============================================================================
# Kiosk Installation Functions
# =============================================================================

kiosk_install_dependencies() {
    log_info "Installing kiosk dependencies..."
    
    apt-get update >> "$KIOSK_LOG_FILE" 2>&1
    
    # Determine correct Chromium package name (varies by Pi OS version)
    local chromium_pkg="chromium-browser"
    if ! apt-cache show chromium-browser &>/dev/null 2>&1; then
        chromium_pkg="chromium"
    fi
    
    local packages=(
        "$chromium_pkg"   # Kiosk browser
        unclutter         # Hide mouse cursor
        xdotool          # X11 automation
        qrencode         # QR code generation
    )
    
    for pkg in "${packages[@]}"; do
        if dpkg -l "$pkg" &>/dev/null 2>&1; then
            log_success "$pkg already installed"
        else
            log_info "Installing $pkg..."
            if apt-get install -y "$pkg" >> "$KIOSK_LOG_FILE" 2>&1; then
                log_success "$pkg installed"
            else
                log_warn "Failed to install $pkg - continuing anyway"
            fi
        fi
    done
}

kiosk_configure_screen_blanking() {
    log_info "Disabling screen blanking..."
    
    local lightdm_conf="/etc/lightdm/lightdm.conf"
    
    if [[ -f "$lightdm_conf" ]]; then
        # Backup original
        cp "$lightdm_conf" "${lightdm_conf}.backup" 2>/dev/null || true
        
        # Check if xserver-command is already set
        if grep -q "^xserver-command=" "$lightdm_conf"; then
            sed -i 's|^xserver-command=.*|xserver-command=X -s 0 -dpms|' "$lightdm_conf"
        else
            # Add under [Seat:*] section
            if grep -q "\[Seat:\*\]" "$lightdm_conf"; then
                sed -i '/\[Seat:\*\]/a xserver-command=X -s 0 -dpms' "$lightdm_conf"
            else
                echo -e "\n[Seat:*]\nxserver-command=X -s 0 -dpms" >> "$lightdm_conf"
            fi
        fi
        
        log_success "Screen blanking disabled in LightDM"
    else
        log_warn "LightDM config not found - screen blanking not configured"
    fi
    
    # Also disable via LXDE autostart
    local lxde_autostart="/etc/xdg/lxsession/LXDE-pi/autostart"
    
    if [[ -f "$lxde_autostart" ]]; then
        cp "$lxde_autostart" "${lxde_autostart}.backup" 2>/dev/null || true
        
        # Remove existing screensaver entries
        sed -i '/@xscreensaver/d' "$lxde_autostart"
        
        # Add screen blanking prevention if not present
        if ! grep -q "@xset s off" "$lxde_autostart"; then
            cat >> "$lxde_autostart" << 'EOF'
@xset s off
@xset -dpms
@xset s noblank
EOF
        fi
        
        log_success "LXDE autostart configured"
    fi
}

kiosk_create_startup_script() {
    log_info "Installing kiosk startup script..."
    
    local pi_home
    pi_home=$(get_pi_home)
    local pi_user
    pi_user=$(get_pi_user)
    
    # Find the script directory - check multiple locations
    local script_dir=""
    local source_script=""
    
    # Try relative to this script
    local this_script_dir
    this_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    if [[ -f "$this_script_dir/start-kiosk.sh" ]]; then
        source_script="$this_script_dir/start-kiosk.sh"
    elif [[ -f "$this_script_dir/../scripts/start-kiosk.sh" ]]; then
        source_script="$this_script_dir/../scripts/start-kiosk.sh"
    elif [[ -n "${LIBRAFOTO_DIR:-}" ]] && [[ -f "$LIBRAFOTO_DIR/scripts/start-kiosk.sh" ]]; then
        source_script="$LIBRAFOTO_DIR/scripts/start-kiosk.sh"
    fi
    
    local kiosk_script="$pi_home/start-kiosk.sh"
    
    if [[ -n "$source_script" ]] && [[ -f "$source_script" ]]; then
        # Copy the kiosk script to user's home directory
        cp "$source_script" "$kiosk_script"
    else
        log_warn "start-kiosk.sh not found, creating default"
        # Create a default kiosk script
        cat > "$kiosk_script" << 'KIOSKEOF'
#!/bin/bash
#
# LibraFoto Kiosk Startup Script
# Starts Chromium in fullscreen kiosk mode

# Wait for desktop to be ready
sleep 5

# Hide mouse cursor
unclutter -idle 0.5 -root &

# Disable screen blanking
xset s off
xset -dpms
xset s noblank

# Kill any existing Chromium instances
pkill -f chromium || true
sleep 1

# Launch Chromium in kiosk mode
chromium-browser \
    --kiosk \
    --noerrdialogs \
    --disable-infobars \
    --no-first-run \
    --enable-features=OverlayScrollbar \
    --start-fullscreen \
    --disable-restore-session-state \
    --disable-session-crashed-bubble \
    --disable-component-update \
    --autoplay-policy=no-user-gesture-required \
    "http://localhost/display/" &

# Keep script running
wait
KIOSKEOF
    fi
    
    chmod +x "$kiosk_script"
    chown "$pi_user:$pi_user" "$kiosk_script"
    
    log_success "Kiosk script installed: $kiosk_script"
}

kiosk_configure_autostart() {
    log_info "Configuring kiosk autostart..."
    
    local pi_home
    pi_home=$(get_pi_home)
    local pi_user
    pi_user=$(get_pi_user)
    local kiosk_script="$pi_home/start-kiosk.sh"
    
    # Method 1: XDG autostart (works with modern Pi OS including Wayfire/Labwc)
    local xdg_autostart_dir="$pi_home/.config/autostart"
    mkdir -p "$xdg_autostart_dir"
    
    cat > "$xdg_autostart_dir/librafoto-kiosk.desktop" << EOF
[Desktop Entry]
Type=Application
Name=LibraFoto Kiosk
Comment=LibraFoto Photo Display Kiosk Mode
Exec=$kiosk_script
Hidden=false
NoDisplay=false
X-GNOME-Autostart-enabled=true
Terminal=false
EOF
    
    chown "$pi_user:$pi_user" "$xdg_autostart_dir/librafoto-kiosk.desktop"
    log_success "Added XDG autostart entry"
    
    # Method 2: LXDE autostart (for older Pi OS with LXDE)
    local lxde_autostart="/etc/xdg/lxsession/LXDE-pi/autostart"
    
    if [[ -f "$lxde_autostart" ]]; then
        # Remove any existing LibraFoto entries
        sed -i '/start-kiosk.sh/d' "$lxde_autostart"
        sed -i '/LibraFoto/d' "$lxde_autostart"
        
        # Add kiosk script to autostart
        echo "@$kiosk_script" >> "$lxde_autostart"
        
        log_success "Added to LXDE autostart"
    fi
    
    # Method 3: User LXDE autostart (backup method)
    local user_lxde_autostart="$pi_home/.config/lxsession/LXDE-pi"
    if [[ -d "$user_lxde_autostart" ]] || [[ -f "$user_lxde_autostart/autostart" ]]; then
        mkdir -p "$user_lxde_autostart"
        local user_autostart_file="$user_lxde_autostart/autostart"
        
        if [[ -f "$user_autostart_file" ]]; then
            if ! grep -q "start-kiosk.sh" "$user_autostart_file"; then
                echo "@$kiosk_script" >> "$user_autostart_file"
            fi
        fi
    fi
    
    chown -R "$pi_user:$pi_user" "$pi_home/.config"
    
    log_success "Autostart configured"
}

kiosk_configure_autologin() {
    log_info "Configuring desktop autologin..."
    
    # Use raspi-config to set boot to desktop with autologin
    if check_command raspi-config; then
        # B4 = Desktop Autologin
        raspi-config nonint do_boot_behaviour B4 >> "$KIOSK_LOG_FILE" 2>&1 || true
        log_success "Desktop autologin enabled"
    else
        log_warn "raspi-config not found - autologin not configured"
    fi
}

kiosk_install_ip_update_service() {
    log_info "Installing host IP update service..."
    
    # Find the update-host-ip.sh script
    local source_script=""
    local this_script_dir
    this_script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    
    if [[ -f "$this_script_dir/update-host-ip.sh" ]]; then
        source_script="$this_script_dir/update-host-ip.sh"
    elif [[ -f "$this_script_dir/../scripts/update-host-ip.sh" ]]; then
        source_script="$this_script_dir/../scripts/update-host-ip.sh"
    elif [[ -n "${LIBRAFOTO_DIR:-}" ]] && [[ -f "$LIBRAFOTO_DIR/scripts/update-host-ip.sh" ]]; then
        source_script="$LIBRAFOTO_DIR/scripts/update-host-ip.sh"
    fi
    
    if [[ -z "$source_script" ]] || [[ ! -f "$source_script" ]]; then
        log_warn "IP update script not found - skipping"
        return 0
    fi
    
    # Make executable
    chmod +x "$source_script"
    
    # Create systemd service
    cat > /etc/systemd/system/librafoto-ip-update.service << EOF
[Unit]
Description=LibraFoto Host IP Update
After=network-online.target
Wants=network-online.target
Before=docker.service

[Service]
Type=oneshot
ExecStart=$source_script
RemainAfterExit=yes

[Install]
WantedBy=multi-user.target
EOF
    
    # Enable service
    systemctl daemon-reload >> "$KIOSK_LOG_FILE" 2>&1
    systemctl enable librafoto-ip-update.service >> "$KIOSK_LOG_FILE" 2>&1
    
    log_success "Host IP update service installed (runs at boot)"
}

# =============================================================================
# Kiosk Removal Functions
# =============================================================================

kiosk_uninstall() {
    log_info "Removing kiosk configuration..."
    
    local pi_home
    pi_home=$(get_pi_home)
    
    # Remove kiosk script
    rm -f "$pi_home/start-kiosk.sh"
    log_info "Removed kiosk startup script"
    
    # Remove XDG autostart entry
    rm -f "$pi_home/.config/autostart/librafoto-kiosk.desktop"
    log_info "Removed XDG autostart entry"
    
    # Remove from LXDE autostart
    local lxde_autostart="/etc/xdg/lxsession/LXDE-pi/autostart"
    if [[ -f "$lxde_autostart" ]]; then
        sed -i '/start-kiosk.sh/d' "$lxde_autostart"
        sed -i '/LibraFoto/d' "$lxde_autostart"
        log_info "Removed from LXDE autostart"
    fi
    
    # Restore lightdm config if backup exists
    if [[ -f "/etc/lightdm/lightdm.conf.backup" ]]; then
        mv /etc/lightdm/lightdm.conf.backup /etc/lightdm/lightdm.conf
        log_info "Restored LightDM configuration"
    fi
    
    # Remove IP update service
    if [[ -f "/etc/systemd/system/librafoto-ip-update.service" ]]; then
        systemctl disable librafoto-ip-update.service >> "$KIOSK_LOG_FILE" 2>&1 || true
        rm -f /etc/systemd/system/librafoto-ip-update.service
        systemctl daemon-reload >> "$KIOSK_LOG_FILE" 2>&1
        log_info "Removed IP update service"
    fi
    
    log_success "Kiosk configuration removed"
}

# =============================================================================
# Kiosk Status Check
# =============================================================================

kiosk_status() {
    echo ""
    echo -e "${BOLD}LibraFoto Kiosk Status${NC}"
    echo "========================"
    echo ""
    
    local pi_home
    pi_home=$(get_pi_home)
    local status_ok=true
    
    # Check kiosk script
    if [[ -f "$pi_home/start-kiosk.sh" ]]; then
        echo -e "${GREEN}✓${NC} Kiosk script installed: $pi_home/start-kiosk.sh"
    else
        echo -e "${RED}✗${NC} Kiosk script not found"
        status_ok=false
    fi
    
    # Check XDG autostart
    if [[ -f "$pi_home/.config/autostart/librafoto-kiosk.desktop" ]]; then
        echo -e "${GREEN}✓${NC} XDG autostart configured"
    else
        echo -e "${YELLOW}○${NC} XDG autostart not configured"
    fi
    
    # Check LXDE autostart
    local lxde_autostart="/etc/xdg/lxsession/LXDE-pi/autostart"
    if [[ -f "$lxde_autostart" ]] && grep -q "start-kiosk.sh" "$lxde_autostart"; then
        echo -e "${GREEN}✓${NC} LXDE autostart configured"
    else
        echo -e "${YELLOW}○${NC} LXDE autostart not configured (may not be needed)"
    fi
    
    # Check Chromium
    if check_command chromium-browser || check_command chromium; then
        echo -e "${GREEN}✓${NC} Chromium installed"
    else
        echo -e "${RED}✗${NC} Chromium not installed"
        status_ok=false
    fi
    
    # Check unclutter
    if check_command unclutter; then
        echo -e "${GREEN}✓${NC} Unclutter installed"
    else
        echo -e "${YELLOW}○${NC} Unclutter not installed"
    fi
    
    # Check IP update service
    if systemctl is-enabled librafoto-ip-update.service &>/dev/null; then
        echo -e "${GREEN}✓${NC} IP update service enabled"
    else
        echo -e "${YELLOW}○${NC} IP update service not enabled"
    fi
    
    echo ""
    
    if [[ "$status_ok" == "true" ]]; then
        echo -e "${GREEN}Kiosk mode is configured${NC}"
        return 0
    else
        echo -e "${YELLOW}Kiosk mode is not fully configured${NC}"
        return 1
    fi
}

# =============================================================================
# Main Kiosk Setup Function (called by install.sh or standalone)
# =============================================================================

kiosk_full_setup() {
    local step_prefix="${1:-Kiosk}"
    
    log_step "$step_prefix" "Kiosk Mode Configuration"
    
    kiosk_install_dependencies
    kiosk_configure_screen_blanking
    kiosk_create_startup_script
    kiosk_configure_autostart
    kiosk_configure_autologin
    kiosk_install_ip_update_service
    
    log_success "Kiosk mode configured successfully"
}

# =============================================================================
# Standalone Execution
# =============================================================================

kiosk_show_banner() {
    show_banner "Kiosk Mode Setup"
}

kiosk_show_help() {
    kiosk_show_banner
    cat << EOF
Usage: sudo bash $0 [OPTIONS]

OPTIONS:
    --help          Show this help message
    --install       Install kiosk mode (default)
    --uninstall     Remove kiosk mode configuration
    --status        Check kiosk mode status
    --repair        Repair/reconfigure kiosk mode

DESCRIPTION:
    This script configures Raspberry Pi kiosk mode for LibraFoto,
    enabling fullscreen display of the photo slideshow on boot.

WHAT IT DOES:
    - Installs Chromium browser
    - Disables screen blanking and power management
    - Configures boot to desktop with auto-login
    - Sets up automatic Chromium launch in kiosk mode

REQUIREMENTS:
    - Raspberry Pi with Raspberry Pi OS Desktop
    - Root privileges (sudo)

EOF
}

# Only run main if executed directly (not sourced)
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    # Check root
    check_root
    
    # Initialize log
    log_init "LibraFoto Kiosk Setup"
    
    # Parse arguments
    action="install"
    
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --help|-h)
                kiosk_show_help
                exit 0
                ;;
            --install)
                action="install"
                shift
                ;;
            --uninstall)
                action="uninstall"
                shift
                ;;
            --status)
                action="status"
                shift
                ;;
            --repair)
                action="install"
                shift
                ;;
            *)
                echo -e "${RED}[ERROR]${NC} Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done
    
    kiosk_show_banner
    
    case "$action" in
        install)
            kiosk_full_setup "1/1"
            echo ""
            log_success "Kiosk mode setup complete!"
            echo ""
            if confirm_prompt "Would you like to reboot now to start kiosk mode?" "Y"; then
                log_info "Rebooting in 5 seconds..."
                sleep 5
                reboot
            else
                echo "Remember to reboot to start kiosk mode: sudo reboot"
            fi
            ;;
        uninstall)
            kiosk_uninstall
            echo ""
            log_success "Kiosk mode removed. Reboot to apply changes."
            ;;
        status)
            kiosk_status
            ;;
    esac
fi

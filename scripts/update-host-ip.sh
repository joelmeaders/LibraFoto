#!/bin/bash
#
# LibraFoto Host IP Update Script
# Detects the current LAN IP and updates the Docker .env file
# Run this at boot to handle dynamic IP changes
#

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$SCRIPT_DIR/docker/.env"
LOG_FILE="/tmp/librafoto-ip-update.log"

log() {
    echo "$(date '+%Y-%m-%d %H:%M:%S') - $1" >> "$LOG_FILE"
}

# Get the primary LAN IP (first non-loopback IPv4)
get_lan_ip() {
    # Try hostname -I first (most reliable on Pi)
    local ip
    ip=$(hostname -I 2>/dev/null | awk '{print $1}')
    
    if [[ -n "$ip" && "$ip" != "127."* ]]; then
        echo "$ip"
        return
    fi
    
    # Fallback to ip route
    ip=$(ip route get 1.1.1.1 2>/dev/null | awk '{print $7; exit}')
    if [[ -n "$ip" ]]; then
        echo "$ip"
        return
    fi
    
    echo ""
}

log "Starting host IP update..."

# Wait for network (up to 30 seconds)
max_attempts=15
attempt=0
while [[ $attempt -lt $max_attempts ]]; do
    CURRENT_IP=$(get_lan_ip)
    if [[ -n "$CURRENT_IP" ]]; then
        break
    fi
    attempt=$((attempt + 1))
    log "Waiting for network... attempt $attempt/$max_attempts"
    sleep 2
done

if [[ -z "$CURRENT_IP" ]]; then
    log "ERROR: Could not determine LAN IP after $max_attempts attempts"
    exit 1
fi

log "Detected LAN IP: $CURRENT_IP"

# Check if .env exists
if [[ ! -f "$ENV_FILE" ]]; then
    log "ERROR: .env file not found at $ENV_FILE"
    exit 1
fi

# Update or add LIBRAFOTO_HOST_IP in .env
if grep -q "^LIBRAFOTO_HOST_IP=" "$ENV_FILE"; then
    # Get current value
    OLD_IP=$(grep "^LIBRAFOTO_HOST_IP=" "$ENV_FILE" | cut -d'=' -f2)
    
    if [[ "$OLD_IP" == "$CURRENT_IP" ]]; then
        log "IP unchanged ($CURRENT_IP), no update needed"
        exit 0
    fi
    
    # Update existing entry
    sed -i "s/^LIBRAFOTO_HOST_IP=.*/LIBRAFOTO_HOST_IP=$CURRENT_IP/" "$ENV_FILE"
    log "Updated LIBRAFOTO_HOST_IP from $OLD_IP to $CURRENT_IP"
else
    # Add new entry
    echo "LIBRAFOTO_HOST_IP=$CURRENT_IP" >> "$ENV_FILE"
    log "Added LIBRAFOTO_HOST_IP=$CURRENT_IP"
fi

# Restart API container to pick up new IP (if Docker is running)
if command -v docker &>/dev/null && docker info &>/dev/null 2>&1; then
    log "Restarting API container..."
    cd "$SCRIPT_DIR/docker"
    docker compose restart api >> "$LOG_FILE" 2>&1
    log "API container restarted"
fi

log "Host IP update complete"

#!/bin/bash
#
# LibraFoto Host IP Update Script
# Detects the current LAN IP and updates the Docker .env file
# Run this at boot to handle dynamic IP changes
#

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ENV_FILE="$SCRIPT_DIR/docker/.env"

# Use persistent log location (fallback to /tmp if /var/log not writable)
LOG_FILE="/var/log/librafoto-ip-update.log"
if ! touch "$LOG_FILE" 2>/dev/null; then
    LOG_FILE="/tmp/librafoto-ip-update.log"
fi

# Rotate log if > 100KB
if [[ -f "$LOG_FILE" ]]; then
    log_size=$(stat -c%s "$LOG_FILE" 2>/dev/null || stat -f%z "$LOG_FILE" 2>/dev/null || echo 0)
    if [[ $log_size -gt 102400 ]]; then
        tail -n 200 "$LOG_FILE" > "${LOG_FILE}.tmp" 2>/dev/null || true
        mv "${LOG_FILE}.tmp" "$LOG_FILE" 2>/dev/null || true
    fi
fi

# Enhanced logging with systemd journal support
log() {
    local level="${2:-INFO}"
    local timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    local msg="[$timestamp] [$level] $1"
    echo "$msg" >> "$LOG_FILE"
    
    # Also log to systemd journal if available
    if command -v systemd-cat &>/dev/null; then
        echo "$1" | systemd-cat -t librafoto-ip-update -p "${level,,}" 2>/dev/null || true
    fi
}

# Get the primary LAN IP with validation and routability check
get_lan_ip() {
    local ip
    
    # Try hostname -I first (most reliable on Pi)
    ip=$(hostname -I 2>/dev/null | awk '{print $1}')
    
    # Validate IP format and exclude loopback/link-local
    if [[ "$ip" =~ ^[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$ ]] && \
       [[ "$ip" != "127."* ]] && [[ "$ip" != "169.254."* ]]; then
        # Verify it''s routable by pinging a public DNS
        if ping -c 1 -W 2 -I "$ip" 8.8.8.8 >/dev/null 2>&1 || \
           ping -c 1 -W 2 -I "$ip" 1.1.1.1 >/dev/null 2>&1; then
            echo "$ip"
            return 0
        fi
    fi
    
    # Fallback to ip route (but still validate)
    ip=$(ip route get 1.1.1.1 2>/dev/null | awk '{print $7; exit}')
    if [[ -n "$ip" ]] && [[ "$ip" != "127."* ]] && [[ "$ip" != "169.254."* ]]; then
        echo "$ip"
        return 0
    fi
    
    return 1
}

# Log boot context for debugging
log "=== LibraFoto IP Update Service ===" "INFO"
log "Boot time: $(uptime -s 2>/dev/null || date)" "INFO"
log "Uptime: $(uptime -p 2>/dev/null || echo ''unknown'')" "INFO"
log "Network interfaces: $(ip -brief addr show 2>/dev/null | grep -v ''lo'' | awk ''{print $1 ": " $3}'' | tr ''\n'' '', '')" "INFO"
log "Default route: $(ip route show default 2>/dev/null || echo ''none'')" "INFO"
log "Starting host IP update..." "INFO"

# Wait for network with exponential backoff (up to 90 seconds for DHCP on network switch)
MAX_WAIT_SECONDS="${LIBRAFOTO_IP_WAIT_TIMEOUT:-90}"
elapsed=0
wait_interval=2

while [[ $elapsed -lt $MAX_WAIT_SECONDS ]]; do
    CURRENT_IP=$(get_lan_ip)
    if [[ -n "$CURRENT_IP" ]]; then
        log "Successfully detected routable IP: $CURRENT_IP (after ${elapsed}s)" "INFO"
        break
    fi
    
    log "Waiting for routable network... (${elapsed}s/${MAX_WAIT_SECONDS}s, next check in ${wait_interval}s)" "INFO"
    sleep "$wait_interval"
    elapsed=$((elapsed + wait_interval))
    
    # Exponential backoff up to 8 seconds
    if [[ $wait_interval -lt 8 ]]; then
        wait_interval=$((wait_interval * 2))
    fi
done

if [[ -z "$CURRENT_IP" ]]; then
    log "ERROR: Could not determine routable LAN IP after ${MAX_WAIT_SECONDS}s" "ERROR"
    log "Network state: $(ip -brief addr show 2>/dev/null | grep -v ''lo'')" "ERROR"
    log "Routing table: $(ip route show 2>/dev/null)" "ERROR"
    exit 1
fi

log "Detected LAN IP: $CURRENT_IP" "INFO"

# Check if .env exists
if [[ ! -f "$ENV_FILE" ]]; then
    log "ERROR: .env file not found at $ENV_FILE" "ERROR"
    exit 1
fi

# Update or add LIBRAFOTO_HOST_IP in .env
if grep -q "^LIBRAFOTO_HOST_IP=" "$ENV_FILE"; then
    # Get current value
    OLD_IP=$(grep "^LIBRAFOTO_HOST_IP=" "$ENV_FILE" | cut -d''='' -f2)
    
    if [[ "$OLD_IP" == "$CURRENT_IP" ]]; then
        log "IP unchanged ($CURRENT_IP), no update needed" "INFO"
        exit 0
    fi
    
    # Update existing entry
    sed -i "s/^LIBRAFOTO_HOST_IP=.*/LIBRAFOTO_HOST_IP=$CURRENT_IP/" "$ENV_FILE"
    log "Updated LIBRAFOTO_HOST_IP from $OLD_IP to $CURRENT_IP" "INFO"
else
    # Add new entry
    echo "LIBRAFOTO_HOST_IP=$CURRENT_IP" >> "$ENV_FILE"
    log "Added LIBRAFOTO_HOST_IP=$CURRENT_IP" "INFO"
fi

# Restart API container to pick up new IP (wait for Docker if needed)
if command -v docker &>/dev/null; then
    log "Waiting for Docker daemon to be ready..." "INFO"
    
    # Wait up to 30 seconds for Docker to be ready
    docker_wait=0
    docker_ready=false
    while [[ $docker_wait -lt 30 ]]; do
        if docker info &>/dev/null 2>&1; then
            docker_ready=true
            log "Docker is ready" "INFO"
            break
        fi
        sleep 2
        docker_wait=$((docker_wait + 2))
    done
    
    if [[ "$docker_ready" == "true" ]]; then
        log "Restarting API container with new IP..." "INFO"
        cd "$SCRIPT_DIR/docker"
        
        if docker compose restart api >> "$LOG_FILE" 2>&1; then
            log "API container restart initiated" "INFO"
            
            # Wait for API container to be healthy (max 60 seconds)
            health_wait=0
            while [[ $health_wait -lt 60 ]]; do
                health_status=$(docker inspect --format=''{{.State.Health.Status}}'' librafoto-api 2>/dev/null || echo "unknown")
                if [[ "$health_status" == "healthy" ]]; then
                    log "API container is healthy with new IP: $CURRENT_IP" "INFO"
                    break
                elif [[ "$health_status" == "unhealthy" ]]; then
                    log "WARNING: API container is unhealthy" "WARN"
                    break
                fi
                sleep 2
                health_wait=$((health_wait + 2))
            done
            
            if [[ $health_wait -ge 60 ]]; then
                log "WARNING: Timeout waiting for API health check" "WARN"
            fi
        else
            log "ERROR: Failed to restart API container" "ERROR"
        fi
    else
        log "WARNING: Docker daemon not ready after 30s - container may have old IP" "WARN"
        log "You may need to manually restart containers: cd $SCRIPT_DIR/docker && docker compose restart" "WARN"
    fi
else
    log "WARNING: Docker command not found - cannot restart API container" "WARN"
fi

log "Host IP update complete" "INFO"
log "===================================" "INFO"
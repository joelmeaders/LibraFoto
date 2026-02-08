#!/bin/bash
#
# LibraFoto Kiosk Startup Script
# Launches Chromium in fullscreen kiosk mode
#
# This script is copied to the user's home directory during installation
# and configured to auto-start on login via XDG autostart.
#

DISPLAY_URL="http://localhost/display/"
LOG_FILE="/tmp/librafoto-kiosk.log"

# Detect available Chromium binary
if command -v chromium &>/dev/null; then
    CHROMIUM_BIN="chromium"
elif command -v chromium-browser &>/dev/null; then
    CHROMIUM_BIN="chromium-browser"
else
    CHROMIUM_BIN="/usr/bin/chromium"
fi

echo "LibraFoto Kiosk starting at $(date)" > "$LOG_FILE"
echo "Using browser: $CHROMIUM_BIN" >> "$LOG_FILE"

# Wait for X server to be ready
sleep 5

# Disable screen blanking
xset s off 2>/dev/null || true
xset s noblank 2>/dev/null || true
xset -dpms 2>/dev/null || true

# Wait for network and containers to be ready
echo "Waiting for LibraFoto to be accessible..." >> "$LOG_FILE"
max_attempts=60
attempt=0
while ! curl -s -o /dev/null -w "%{http_code}" "$DISPLAY_URL" | grep -q "200\|304"; do
    attempt=$((attempt + 1))
    if [[ $attempt -ge $max_attempts ]]; then
        echo "Timeout waiting for LibraFoto - starting browser anyway" >> "$LOG_FILE"
        break
    fi
    sleep 2
done

echo "Starting Chromium kiosk mode..." >> "$LOG_FILE"

# Hide the mouse cursor
unclutter -idle 0.5 -root &

# Clear Chromium crash flags to prevent recovery popup
CHROMIUM_FLAGS_DIR="$HOME/.config/chromium/Default"
if [[ -d "$CHROMIUM_FLAGS_DIR" ]]; then
    sed -i 's/"exited_cleanly":false/"exited_cleanly":true/' "$CHROMIUM_FLAGS_DIR/Preferences" 2>/dev/null || true
    sed -i 's/"exit_type":"Crashed"/"exit_type":"Normal"/' "$CHROMIUM_FLAGS_DIR/Preferences" 2>/dev/null || true
fi

# Launch Chromium in kiosk mode
exec "$CHROMIUM_BIN" \
    --kiosk \
    --noerrdialogs \
    --disable-infobars \
    --password-store=basic \
    --disable-session-crashed-bubble \
    --disable-restore-session-state \
    --disable-features=TranslateUI \
    --check-for-update-interval=31536000 \
    --disable-component-update \
    --disable-pinch \
    --overscroll-history-navigation=0 \
    --disable-features=TouchpadOverscrollHistoryNavigation \
    --autoplay-policy=no-user-gesture-required \
    --no-first-run \
    --no-default-browser-check \
    --disk-cache-size=1 \
    --disk-cache-dir=/tmp/chromium-cache \
    --disable-background-networking \
    --disable-sync \
    "$DISPLAY_URL"

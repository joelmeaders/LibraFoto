#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

for test_file in \
    "$THIS_DIR/common_test.sh" \
    "$THIS_DIR/install_test.sh" \
    "$THIS_DIR/update_test.sh" \
    "$THIS_DIR/uninstall_test.sh" \
    "$THIS_DIR/kiosk_setup_test.sh" \
    "$THIS_DIR/start_kiosk_test.sh" \
    "$THIS_DIR/update_host_ip_test.sh"; do
    echo "Running $(basename "$test_file")"
    bash "$test_file"
    echo ""
done

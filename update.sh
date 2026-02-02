#!/bin/bash
#
# LibraFoto - Update Script
#
# This script automates updating LibraFoto:
# - Check for available updates
# - Backup database and configuration
# - Pull latest code changes
# - Run database migrations
# - Rebuild and deploy containers
# - Rollback on failure
#
# Usage:
#   ./update.sh           # Interactive update
#   ./update.sh --check   # Check for updates only
#   ./update.sh --force   # Update without confirmation
#   ./update.sh --help    # Show help
#
# Version: 1.0.0
# Repository: https://github.com/librafoto/librafoto

set -euo pipefail

# =============================================================================
# Configuration
# =============================================================================

readonly SCRIPT_VERSION="1.0.0"
readonly LOG_FILE="/tmp/librafoto-update.log"
readonly BACKUP_DIR="${BACKUP_DIR:-./backups}"
readonly HEALTH_CHECK_TIMEOUT=120
readonly HEALTH_CHECK_INTERVAL=5

# Cache the script directory at startup (must be done before any cd commands)
# This ensures get_script_dir returns the correct path even after directory changes
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
# Utility Functions
# =============================================================================

get_script_dir() {
    echo "$SCRIPT_DIR"
}

# =============================================================================
# Banner and Help
# =============================================================================

# Update-specific banner
show_update_banner() {
    show_banner "Update Script"
}

show_help() {
    show_update_banner
    cat << EOF
Usage: $0 [OPTIONS]

OPTIONS:
    --help, -h      Show this help message
    --check         Check for updates without installing
    --force, -f     Update without confirmation prompts
    --no-cache      Force rebuild without Docker cache
    --rollback      Rollback to previous version from backup
    --repair-kiosk  Repair kiosk mode configuration

DESCRIPTION:
    This script automates the LibraFoto update process including:
    - Pre-update backup of database and configuration
    - Git pull to fetch latest changes
    - Database migrations
    - Container rebuild and deployment
    - Health checks with automatic rollback on failure

EXAMPLES:
    ./update.sh              # Interactive update
    ./update.sh --check      # Check for available updates
    ./update.sh --force      # Non-interactive update
    ./update.sh --rollback   # Restore from last backup

BACKUP LOCATION:
    Backups are stored in: $BACKUP_DIR

LOG FILE:
    Update log is saved to: $LOG_FILE

EOF
}

# =============================================================================
# Pre-flight Checks
# =============================================================================

check_prerequisites() {
    local script_dir
    script_dir=$(get_script_dir)
    
    # Check if user can run docker (either root or in docker group)
    if ! docker info &>/dev/null; then
        log_error "Cannot access Docker. Either run with sudo or add your user to the docker group:"
        echo "  sudo usermod -aG docker \$USER"
        echo "  (Then log out and back in)"
        exit 1
    fi
    
    # Check for git
    if ! check_command git; then
        log_error "Git is not installed"
        exit 1
    fi
    
    # Check for docker
    if ! check_command docker; then
        log_error "Docker is not installed"
        exit 1
    fi
    
    # Check if in git repository
    if ! git -C "$script_dir" rev-parse --git-dir &>/dev/null; then
        log_error "Not a git repository. Cannot check for updates."
        log_info "If you installed without git, download the latest release manually."
        exit 1
    fi
    
    # Check for docker-compose.yml
    if [[ ! -f "$script_dir/docker/docker-compose.yml" ]]; then
        log_error "docker/docker-compose.yml not found"
        exit 1
    fi
    
    log_success "Prerequisites verified"
}

# =============================================================================
# Update Check
# =============================================================================

check_for_updates() {
    local script_dir
    script_dir=$(get_script_dir)
    
    log_info "Checking for updates..."
    
    cd "$script_dir"
    
    # Fetch latest from remote
    if ! git fetch origin --quiet 2>> "$LOG_FILE"; then
        log_error "Failed to fetch from remote repository"
        return 1
    fi
    
    local current_commit
    current_commit=$(git rev-parse HEAD)
    
    local remote_commit
    remote_commit=$(git rev-parse origin/main 2>/dev/null || git rev-parse origin/master 2>/dev/null)
    
    if [[ "$current_commit" == "$remote_commit" ]]; then
        log_success "Already up to date"
        echo ""
        echo -e "Current version: ${BOLD}$(get_current_version "$script_dir")${NC}"
        echo -e "Current commit:  ${BOLD}$(get_current_commit)${NC}"
        return 1
    fi
    
    # Count commits behind
    local commits_behind
    commits_behind=$(git rev-list --count HEAD..origin/main 2>/dev/null || git rev-list --count HEAD..origin/master 2>/dev/null || echo "?")
    
    echo ""
    echo -e "${GREEN}${BOLD}Update available!${NC}"
    echo ""
    echo -e "Current version: ${BOLD}$(get_current_version "$script_dir")${NC} ($(get_current_commit))"
    echo -e "Commits behind:  ${BOLD}$commits_behind${NC}"
    echo ""
    
    # Show changelog (last 10 commits)
    echo -e "${BOLD}Recent changes:${NC}"
    echo ""
    git log --oneline HEAD..origin/main 2>/dev/null | head -10 || \
    git log --oneline HEAD..origin/master 2>/dev/null | head -10 || \
    echo "  (Unable to retrieve changelog)"
    echo ""
    
    return 0
}

# =============================================================================
# Backup Functions
# =============================================================================

create_backup() {
    log_step "1/5" "Creating Backup"
    
    local script_dir
    script_dir=$(get_script_dir)
    local timestamp
    timestamp=$(date +%Y%m%d-%H%M%S)
    local backup_path="$script_dir/$BACKUP_DIR/$timestamp"
    
    # Create backup directory
    mkdir -p "$backup_path"
    log_info "Backup directory: $backup_path"
    
    # Save current version info
    echo "$(get_current_version "$script_dir")" > "$backup_path/version.txt"
    echo "$(get_current_commit)" > "$backup_path/commit.txt"
    echo "$(date -Iseconds)" > "$backup_path/timestamp.txt"
    
    # Backup database via Docker volume
    log_info "Backing up database..."
    
    if docker volume ls | grep -q "librafoto-data"; then
        if docker run --rm \
            -v librafoto-data:/data \
            -v "$backup_path":/backup \
            alpine tar czf /backup/data-backup.tar.gz /data 2>> "$LOG_FILE"; then
            log_success "Database backed up"
        else
            log_warn "Database backup failed - volume may be empty"
        fi
    else
        log_warn "Volume 'librafoto-data' not found - skipping database backup"
    fi
    
    # Backup docker .env file
    if [[ -f "$script_dir/docker/.env" ]]; then
        cp "$script_dir/docker/.env" "$backup_path/docker-env.backup"
        log_success "Configuration backed up"
    fi
    
    # Save current container image IDs for rollback
    log_info "Recording current container versions..."
    docker images --format "{{.Repository}}:{{.Tag}} {{.ID}}" | grep librafoto > "$backup_path/images.txt" 2>/dev/null || true
    
    # Record current git state
    git -C "$script_dir" rev-parse HEAD > "$backup_path/git-head.txt"
    git -C "$script_dir" stash list > "$backup_path/git-stash.txt" 2>/dev/null || true
    
    # Create latest symlink
    ln -sfn "$timestamp" "$script_dir/$BACKUP_DIR/latest"
    
    log_success "Backup completed: $backup_path"
    echo "$backup_path"
}

list_backups() {
    local script_dir
    script_dir=$(get_script_dir)
    local backup_base="$script_dir/$BACKUP_DIR"
    
    if [[ ! -d "$backup_base" ]]; then
        echo "No backups found"
        return 1
    fi
    
    echo -e "${BOLD}Available backups:${NC}"
    echo ""
    
    for backup in "$backup_base"/*/; do
        if [[ -d "$backup" && "$(basename "$backup")" != "latest" ]]; then
            local name
            name=$(basename "$backup")
            local version="unknown"
            local commit="unknown"
            
            [[ -f "$backup/version.txt" ]] && version=$(cat "$backup/version.txt")
            [[ -f "$backup/commit.txt" ]] && commit=$(cat "$backup/commit.txt")
            
            local is_latest=""
            if [[ -L "$backup_base/latest" ]] && [[ "$(readlink "$backup_base/latest")" == "$name" ]]; then
                is_latest=" ${GREEN}(latest)${NC}"
            fi
            
            echo -e "  $name - v$version ($commit)$is_latest"
        fi
    done
    echo ""
}

# =============================================================================
# Git Update
# =============================================================================

pull_updates() {
    local force_mode="${1:-false}"
    
    log_step "2/5" "Pulling Updates"
    
    local script_dir
    script_dir=$(get_script_dir)
    
    cd "$script_dir"
    
    # Check for local changes
    if ! git diff-index --quiet HEAD -- 2>/dev/null; then
        log_warn "Local changes detected"
        
        if [[ "$force_mode" == "true" ]] || confirm_prompt "Stash local changes and continue?" "Y"; then
            git stash push -m "Auto-stash before update $(date +%Y%m%d-%H%M%S)" >> "$LOG_FILE" 2>&1
            log_info "Local changes stashed"
        else
            log_error "Update cancelled - please commit or stash your changes"
            return 1
        fi
    fi
    
    # Pull latest changes
    log_info "Pulling latest changes..."
    
    local branch
    branch=$(git rev-parse --abbrev-ref HEAD)
    
    if ! git pull origin "$branch" >> "$LOG_FILE" 2>&1; then
        log_error "Git pull failed"
        log_info "Check $LOG_FILE for details"
        
        # Attempt to recover
        if git status | grep -q "You have unmerged paths"; then
            log_error "Merge conflict detected. Please resolve manually."
            git merge --abort >> "$LOG_FILE" 2>&1 || true
        fi
        
        return 1
    fi
    
    # Update submodules if any
    if [[ -f "$script_dir/.gitmodules" ]]; then
        log_info "Updating submodules..."
        git submodule update --init --recursive >> "$LOG_FILE" 2>&1 || true
    fi
    
    local new_version
    new_version=$(get_current_version "$script_dir")
    local new_commit
    new_commit=$(get_current_commit)
    
    log_success "Updated to version $new_version ($new_commit)"
}

# =============================================================================
# Database Migration
# =============================================================================

run_migrations() {
    log_step "3/5" "Database Migrations"
    
    local script_dir
    script_dir=$(get_script_dir)
    
    cd "$script_dir/docker"
    
    # LibraFoto uses automatic migrations on startup via EF Core
    # The production container doesn't include EF tools, so migrations
    # are applied when the API container starts
    
    log_info "Migrations will be applied automatically on container startup"
    log_info "EF Core handles this via DbContext.Database.Migrate()"
}

# =============================================================================
# Container Rebuild
# =============================================================================

rebuild_containers() {
    local no_cache="$1"
    
    log_step "4/5" "Rebuilding Containers"
    
    local script_dir
    script_dir=$(get_script_dir)
    
    cd "$script_dir/docker"
    
    # Fix permissions on data/backups folders if they exist
    # Docker buildkit needs read access to scan directories, even if they're in .dockerignore
    # These folders may be root-owned from container operations
    for dir in "$script_dir/data" "$script_dir/backups"; do
        if [[ -d "$dir" ]]; then
            local current_perms
            current_perms=$(stat -c "%a" "$dir" 2>/dev/null || echo "000")
            if [[ ! "$current_perms" =~ ^7[0-7][5-7]$ ]]; then
                log_info "Fixing permissions on $(basename "$dir") folder..."
                chmod 755 "$dir" 2>/dev/null || sudo chmod 755 "$dir" 2>/dev/null || {
                    log_warn "Could not fix permissions on $dir - docker build may fail"
                    log_info "Run: sudo chmod 755 $dir"
                }
            fi
        fi
    done
    
    # Enable BuildKit
    export DOCKER_BUILDKIT=1
    export COMPOSE_DOCKER_CLI_BUILD=1
    
    local build_args=""
    if [[ "$no_cache" == "true" ]]; then
        build_args="--no-cache"
        log_info "Building with --no-cache"
    fi
    
    # Get version for build
    local version
    version=$(get_current_version "$script_dir")
    export VERSION="$version"
    
    log_info "Building containers (version: $version)..."
    echo ""
    
    if ! docker compose build $build_args 2>&1 | tee -a "$LOG_FILE"; then
        log_error "Container build failed"
        return 1
    fi
    
    log_success "Containers rebuilt successfully"
}

# =============================================================================
# Deploy Containers
# =============================================================================

deploy_containers() {
    log_step "5/5" "Deploying Containers"
    
    local script_dir
    script_dir=$(get_script_dir)
    
    cd "$script_dir/docker"
    
    # Store current container IDs for potential rollback
    local old_containers
    old_containers=$(docker compose ps -q 2>/dev/null || true)
    
    log_info "Stopping existing containers..."
    docker compose down >> "$LOG_FILE" 2>&1 || true
    
    log_info "Starting updated containers..."
    if ! docker compose up -d 2>&1 | tee -a "$LOG_FILE"; then
        log_error "Failed to start containers"
        return 1
    fi
    
    # Wait for containers to be healthy
    log_info "Waiting for services to be healthy..."
    
    local wait_time=0
    local all_healthy=false
    
    while [[ $wait_time -lt $HEALTH_CHECK_TIMEOUT ]]; do
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
        
        sleep $HEALTH_CHECK_INTERVAL
        wait_time=$((wait_time + HEALTH_CHECK_INTERVAL))
        echo -n "."
    done
    echo ""
    
    if [[ "$all_healthy" == "true" ]]; then
        log_success "All services are running and healthy"
        return 0
    else
        log_error "Health check failed after ${HEALTH_CHECK_TIMEOUT}s"
        
        echo ""
        log_info "Container status:"
        docker compose ps
        
        echo ""
        log_info "Recent logs:"
        docker compose logs --tail=20 api 2>/dev/null || true
        
        return 1
    fi
}

# =============================================================================
# Rollback
# =============================================================================

perform_rollback() {
    local backup_name="${1:-latest}"
    
    log_step "ROLLBACK" "Restoring from backup"
    
    local script_dir
    script_dir=$(get_script_dir)
    local backup_path
    
    if [[ "$backup_name" == "latest" ]]; then
        if [[ -L "$script_dir/$BACKUP_DIR/latest" ]]; then
            backup_path="$script_dir/$BACKUP_DIR/$(readlink "$script_dir/$BACKUP_DIR/latest")"
        else
            log_error "No 'latest' backup symlink found"
            list_backups
            return 1
        fi
    else
        backup_path="$script_dir/$BACKUP_DIR/$backup_name"
    fi
    
    if [[ ! -d "$backup_path" ]]; then
        log_error "Backup not found: $backup_path"
        list_backups
        return 1
    fi
    
    log_info "Rolling back from: $backup_path"
    
    # Confirm rollback
    if ! confirm_prompt "This will restore the database and revert code. Continue?" "N"; then
        echo "Rollback cancelled"
        return 0
    fi
    
    cd "$script_dir"
    
    # Stop containers
    log_info "Stopping containers..."
    docker compose -f docker/docker-compose.yml down >> "$LOG_FILE" 2>&1 || true
    
    # Restore git state
    if [[ -f "$backup_path/git-head.txt" ]]; then
        local old_commit
        old_commit=$(cat "$backup_path/git-head.txt")
        log_info "Reverting to commit: $old_commit"
        
        git checkout "$old_commit" >> "$LOG_FILE" 2>&1 || {
            log_warn "Could not checkout old commit - you may need to do this manually"
        }
    fi
    
    # Restore database
    if [[ -f "$backup_path/data-backup.tar.gz" ]]; then
        log_info "Restoring database..."
        
        # Remove existing volume and recreate
        docker volume rm librafoto-data 2>/dev/null || true
        docker volume create librafoto-data >> "$LOG_FILE" 2>&1
        
        docker run --rm \
            -v librafoto-data:/data \
            -v "$backup_path":/backup \
            alpine sh -c "cd / && tar xzf /backup/data-backup.tar.gz" >> "$LOG_FILE" 2>&1
        
        log_success "Database restored"
    fi
    
    # Restore configuration
    if [[ -f "$backup_path/docker-env.backup" ]]; then
        cp "$backup_path/docker-env.backup" "$script_dir/docker/.env"
        log_success "Configuration restored"
    fi
    
    # Rebuild and restart
    log_info "Rebuilding containers..."
    cd "$script_dir/docker"
    docker compose build >> "$LOG_FILE" 2>&1
    docker compose up -d >> "$LOG_FILE" 2>&1
    
    log_success "Rollback completed"
    echo ""
    echo "Please verify the application is working correctly."
    echo "You may need to run: git checkout main (or your branch) after verification."
}

automatic_rollback() {
    log_error "Deployment failed - initiating automatic rollback"
    
    local script_dir
    script_dir=$(get_script_dir)
    
    # Check if we have a backup
    if [[ ! -L "$script_dir/$BACKUP_DIR/latest" ]]; then
        log_error "No backup available for automatic rollback"
        log_info "Please investigate the issue manually"
        return 1
    fi
    
    perform_rollback "latest"
}

# =============================================================================
# Cleanup
# =============================================================================

cleanup_old_backups() {
    local script_dir
    script_dir=$(get_script_dir)
    local backup_base="$script_dir/$BACKUP_DIR"
    local keep_count=5
    
    if [[ ! -d "$backup_base" ]]; then
        return 0
    fi
    
    # Count backups
    local backup_count
    backup_count=$(find "$backup_base" -maxdepth 1 -type d -name "20*" | wc -l)
    
    if [[ $backup_count -gt $keep_count ]]; then
        log_info "Cleaning up old backups (keeping last $keep_count)..."
        
        # Remove oldest backups
        find "$backup_base" -maxdepth 1 -type d -name "20*" | sort | head -n -$keep_count | while read -r old_backup; do
            rm -rf "$old_backup"
            log_info "Removed: $(basename "$old_backup")"
        done
    fi
}

# =============================================================================
# Post-Update
# =============================================================================

show_post_update() {
    local script_dir
    script_dir=$(get_script_dir)
    
    echo ""
    echo -e "${GREEN}${BOLD}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}${BOLD}║              LibraFoto Update Complete!                    ║${NC}"
    echo -e "${GREEN}${BOLD}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    echo -e "${BOLD}Version:${NC} $(get_current_version "$script_dir") ($(get_current_commit))"
    echo ""
    
    echo -e "${BOLD}Container Status:${NC}"
    docker compose -f "$script_dir/docker/docker-compose.yml" ps
    echo ""
    
    echo -e "${BOLD}Access URLs:${NC}"
    local ip_address
    ip_address=$(get_ip_address)
    echo "  Display UI: http://$ip_address/display/"
    echo "  Admin UI:   http://$ip_address/admin/"
    echo ""
    
    echo -e "${BOLD}Log file:${NC} $LOG_FILE"
    echo ""
    
    # Cleanup old backups
    cleanup_old_backups
}

# =============================================================================
# Main Entry Point
# =============================================================================

main() {
    local check_only=false
    local force_update=false
    local no_cache=false
    local do_rollback=false
    local rollback_target="latest"
    local repair_kiosk=false
    
    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case "$1" in
            --help|-h)
                show_help
                exit 0
                ;;
            --check)
                check_only=true
                shift
                ;;
            --force|-f)
                force_update=true
                shift
                ;;
            --no-cache)
                no_cache=true
                shift
                ;;
            --rollback)
                do_rollback=true
                if [[ $# -gt 1 ]] && [[ ! "$2" =~ ^-- ]]; then
                    rollback_target="$2"
                    shift
                fi
                shift
                ;;
            --list-backups)
                list_backups
                exit 0
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
    log_init "LibraFoto Update"
    show_update_banner
    
    # Check prerequisites
    check_prerequisites
    
    # Handle rollback
    if [[ "$do_rollback" == "true" ]]; then
        perform_rollback "$rollback_target"
        exit $?
    fi
    
    # Handle kiosk repair
    if [[ "$repair_kiosk" == "true" ]]; then
        log_info "Repairing kiosk mode..."
        local script_dir
        script_dir=$(get_script_dir)
        
        # Try kiosk-setup.sh first (preferred), fall back to install.sh
        if [[ -f "$script_dir/scripts/kiosk-setup.sh" ]]; then
            bash "$script_dir/scripts/kiosk-setup.sh" --repair
            exit $?
        elif [[ -f "$script_dir/install.sh" ]]; then
            bash "$script_dir/install.sh" --repair-kiosk
            exit $?
        else
            log_error "Kiosk setup script not found"
            exit 1
        fi
    fi
    
    # Check for updates
    if ! check_for_updates; then
        exit 0
    fi
    
    # Exit if only checking
    if [[ "$check_only" == "true" ]]; then
        exit 0
    fi
    
    # Confirm update
    if [[ "$force_update" != "true" ]]; then
        if ! confirm_prompt "Do you want to install this update?" "Y"; then
            echo "Update cancelled"
            exit 0
        fi
    fi
    
    echo ""
    
    # Track if we need to rollback on failure
    local backup_path=""
    local update_failed=false
    
    # Create backup (always enabled)
    backup_path=$(create_backup)
    
    # Pull updates
    if ! pull_updates "$force_update"; then
        update_failed=true
    fi
    
    # Run migrations (if pull succeeded)
    if [[ "$update_failed" != "true" ]]; then
        run_migrations || true  # Migrations are non-fatal
    fi
    
    # Rebuild containers
    if [[ "$update_failed" != "true" ]]; then
        if ! rebuild_containers "$no_cache"; then
            update_failed=true
        fi
    fi
    
    # Deploy containers
    if [[ "$update_failed" != "true" ]]; then
        if ! deploy_containers; then
            update_failed=true
        fi
    fi
    
    # Handle failure
    if [[ "$update_failed" == "true" ]]; then
        echo ""
        log_error "Update failed"
        
        if [[ -n "$backup_path" ]]; then
            if confirm_prompt "Would you like to rollback to the previous version?" "Y"; then
                automatic_rollback
            else
                log_warn "Rollback skipped. You can manually rollback later with:"
                echo "  sudo ./update.sh --rollback"
            fi
        fi
        
        exit 1
    fi
    
    # Success
    show_post_update
}

# Run main function
main "$@"

#!/bin/bash
#
# LibraFoto - Update Script
#
# This script automates updating LibraFoto with a fully interactive workflow:
# - Check for available updates
# - Show preview of changes
# - Interactive configuration prompts
# - Backup database and configuration
# - Pull latest code changes
# - Run database migrations
# - Rebuild and deploy containers
# - Automatic rollback on failure
#
# Usage:
#   ./update.sh           # Interactive update workflow
#   ./update.sh --check   # Check for updates only
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
    --list-backups  List available backups

DESCRIPTION:
    This script provides a fully interactive update workflow for LibraFoto.
    
    The update process includes:
    - Pre-update backup of database and configuration
    - Git pull to fetch latest changes
    - Container rebuild (build mode) or image pull (GHCR mode)
    - Automatic database migrations
    - Health checks and validation

    You'll see a detailed preview of what will change, then be asked:
    1. Whether to proceed with the update
    2. Whether to stash local changes (if any detected)
    3. Whether to switch deploy mode (build ↔ GHCR)
    4. Whether to use Docker build cache (build mode only)

    Deploy Modes:
    - Build mode:  Pulls source code and rebuilds container images locally
    - GHCR mode:   Pulls source code (for scripts) and pre-built images from
                   GitHub Container Registry

    If the update fails, you'll be offered an automatic rollback to restore
    your previous version.

EXAMPLES:
    ./update.sh              # Interactive update workflow
    ./update.sh --check      # Check for available updates only
    ./update.sh --list-backups  # Show available backups

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
    local deploy_mode
    deploy_mode=$(get_deploy_mode "$script_dir")
    
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
    
    # Check for docker-compose.yml (always needed)
    if [[ ! -f "$script_dir/docker/docker-compose.yml" ]]; then
        log_error "docker/docker-compose.yml not found"
        exit 1
    fi
    
    # Check for GHCR compose file if in GHCR mode
    if [[ "$deploy_mode" == "ghcr" ]] && [[ ! -f "$script_dir/docker/docker-compose.ghcr.yml" ]]; then
        log_warn "docker-compose.ghcr.yml not found - will be available after git pull"
    fi
    
    log_info "Deploy mode: $([ "$deploy_mode" == "ghcr" ] && echo "Pre-built GitHub images" || echo "Local source build")"
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

# Show preview of what will be updated
show_update_preview() {
    local script_dir="${1:-.}"
    local deploy_mode="$2"
    local commits_behind="${3:-?}"
    
    echo ""
    echo -e "${BOLD}═══════════════════════════════════════════════════════${NC}"
    echo -e "${BOLD}Update Preview${NC}"
    echo -e "${BOLD}═══════════════════════════════════════════════════════${NC}"
    
    echo -e "\n${BOLD}Current State:${NC}"
    echo "  • Version: $(get_current_version "$script_dir") ($(get_current_commit))"
    echo "  • Deploy Mode: $([ "$deploy_mode" == "ghcr" ] && echo "Pre-built GitHub images" || echo "Local source build")"
    echo "  • Commits Behind: $commits_behind"
    
    echo -e "\n${BOLD}Backup (Step 1/5):${NC}"
    echo "  • New backup created: $script_dir/$BACKUP_DIR/[timestamp]/"
    echo "  • Contents: database, .env, git state, Docker images list"
    
    echo -e "\n${BOLD}Git Changes (Step 2/5):${NC}"
    echo "  • Repository will be updated to latest commit"
    echo "  • Local changes will be stashed if present"
    echo "  • Scripts and configuration files updated"
    
    echo -e "\n${BOLD}Database Migrations (Step 3/5):${NC}"
    echo "  • Migrations applied automatically on API container startup"
    echo "  • EF Core handles via DbContext.Database.Migrate()"
    
    if [[ "$deploy_mode" == "ghcr" ]]; then
        echo -e "\n${BOLD}Container Images (Step 4/5):${NC}"
        echo "  • Pull pre-built images from GitHub Container Registry"
        local version
        version=$(get_current_version "$script_dir")
        local image_tag
        image_tag=$(get_image_tag_for_version "$version")
        echo "  • Image tag: $image_tag"
    else
        echo -e "\n${BOLD}Container Images (Step 4/5):${NC}"
        echo "  • Rebuild images locally from source code"
        echo "  • Estimated time: 5-20 minutes depending on Pi model"
    fi
    
    echo -e "\n${BOLD}Deployment (Step 5/5):${NC}"
    echo "  • Stop existing containers"
    echo "  • Start updated containers"
    echo "  • Wait for health checks (up to 120s)"
    
    echo -e "\n${BOLD}Containers to Restart:${NC}"
    local compose_file
    compose_file=$(get_compose_file "$script_dir")
    if [[ -f "$compose_file" ]]; then
        docker compose -f "$compose_file" ps --format "    • {{.Name}} ({{.Status}})" 2>/dev/null || echo "    • Unable to list containers"
    else
        echo "    • librafoto-api, librafoto-admin, librafoto-display, librafoto-proxy"
    fi
    
    echo -e "\n${BOLD}═══════════════════════════════════════════════════════${NC}\n"
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
    
    local script_dir
    script_dir=$(get_script_dir)
    local deploy_mode
    deploy_mode=$(get_deploy_mode "$script_dir")
    local compose_file
    compose_file=$(get_compose_filename "$script_dir")
    
    cd "$script_dir/docker"
    
    if [[ "$deploy_mode" == "ghcr" ]]; then
        log_step "4/5" "Pulling Updated Images"
        
        # Update image tag in .env based on current .version
        local version
        version=$(get_current_version "$script_dir")
        local image_tag
        image_tag=$(get_image_tag_for_version "$version")
        
        set_env_var "LIBRAFOTO_IMAGE_TAG" "$image_tag" "$script_dir"
        set_env_var "VERSION" "$version" "$script_dir"
        
        log_info "Pulling images (tag: $image_tag)..."
        echo ""
        
        if ! docker compose -f "$compose_file" pull 2>&1 | tee -a "$LOG_FILE"; then
            log_error "Image pull failed"
            return 1
        fi
        
        log_success "Images pulled successfully"
    else
        log_step "4/5" "Rebuilding Containers"
        
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
        
        if ! docker compose -f "$compose_file" build $build_args 2>&1 | tee -a "$LOG_FILE"; then
            log_error "Container build failed"
            return 1
        fi
        
        log_success "Containers rebuilt successfully"
    fi
}

# =============================================================================
# Deploy Containers
# =============================================================================

deploy_containers() {
    log_step "5/5" "Deploying Containers"
    
    local script_dir
    script_dir=$(get_script_dir)
    local compose_file
    compose_file=$(get_compose_filename "$script_dir")
    
    cd "$script_dir/docker"
    
    # Store current container IDs for potential rollback
    local old_containers
    old_containers=$(docker compose -f "$compose_file" ps -q 2>/dev/null || true)
    
    log_info "Stopping existing containers..."
    docker compose -f "$compose_file" down >> "$LOG_FILE" 2>&1 || true
    
    log_info "Starting updated containers..."
    if ! docker compose -f "$compose_file" up -d 2>&1 | tee -a "$LOG_FILE"; then
        log_error "Failed to start containers"
        return 1
    fi
    
    # Wait for containers to be healthy
    log_info "Waiting for services to be healthy..."
    
    local wait_time=0
    local all_healthy=false
    
    while [[ $wait_time -lt $HEALTH_CHECK_TIMEOUT ]]; do
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
        docker compose -f "$compose_file" ps
        
        echo ""
        log_info "Recent logs:"
        docker compose -f "$compose_file" logs --tail=20 api 2>/dev/null || true
        
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
    
    # Stop containers using the appropriate compose file
    local compose_file
    compose_file=$(get_compose_filename "$script_dir")
    
    log_info "Stopping containers..."
    docker compose -f "docker/$compose_file" down >> "$LOG_FILE" 2>&1 || true
    
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
    
    # Rebuild/pull and restart using the appropriate compose file
    local deploy_mode
    deploy_mode=$(get_deploy_mode "$script_dir")
    local rb_compose_file
    rb_compose_file=$(get_compose_filename "$script_dir")
    
    if [[ "$deploy_mode" == "ghcr" ]]; then
        log_info "Pulling container images..."
        cd "$script_dir/docker"
        docker compose -f "$rb_compose_file" pull >> "$LOG_FILE" 2>&1
        docker compose -f "$rb_compose_file" up -d >> "$LOG_FILE" 2>&1
    else
        log_info "Rebuilding containers..."
        cd "$script_dir/docker"
        docker compose -f "$rb_compose_file" build >> "$LOG_FILE" 2>&1
        docker compose -f "$rb_compose_file" up -d >> "$LOG_FILE" 2>&1
    fi
    
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

validate_update() {
    local script_dir="${1:-.}"
    local backup_path="$2"
    
    echo -e "\n${BOLD}Validating Update:${NC}\n"
    
    local validation_passed=true
    
    # Check version changed
    if [[ -f "$backup_path/version.txt" ]]; then
        local old_version
        old_version=$(cat "$backup_path/version.txt")
        local new_version
        new_version=$(get_current_version "$script_dir")
        
        if [[ "$old_version" != "$new_version" ]]; then
            echo -e "  ${GREEN}✓${NC} Version updated: $old_version → $new_version"
        else
            echo -e "  ${YELLOW}⚠${NC} Version unchanged (may be commit-only update)"
        fi
    fi
    
    # Check containers healthy
    if check_docker; then
        local compose_file
        compose_file=$(get_compose_filename "$script_dir")
        local api_health
        api_health=$(docker inspect librafoto-api --format='{{.State.Health.Status}}' 2>/dev/null || echo "unknown")
        
        if [[ "$api_health" == "healthy" ]]; then
            echo -e "  ${GREEN}✓${NC} API container healthy"
        else
            echo -e "  ${RED}✗${NC} API container not healthy (status: $api_health)"
            validation_passed=false
        fi
        
        local running_count
        running_count=$(cd "$script_dir/docker" && docker compose -f "$compose_file" ps --status running -q 2>/dev/null | wc -l)
        local expected_count
        expected_count=$(cd "$script_dir/docker" && docker compose -f "$compose_file" config --services 2>/dev/null | wc -l)
        
        if [[ $running_count -ge $expected_count ]] && [[ $expected_count -gt 0 ]]; then
            echo -e "  ${GREEN}✓${NC} All containers running ($running_count/$expected_count)"
        else
            echo -e "  ${YELLOW}⚠${NC} Some containers not running ($running_count/$expected_count)"
        fi
    fi
    
    # Check backup created
    if [[ -f "$backup_path/data-backup.tar.gz" ]]; then
        local backup_size
        backup_size=$(du -sh "$backup_path/data-backup.tar.gz" 2>/dev/null | cut -f1 || echo "unknown")
        echo -e "  ${GREEN}✓${NC} Backup created ($backup_size)"
    else
        echo -e "  ${YELLOW}⚠${NC} No data backup found (volume may be empty)"
    fi
    
    echo ""
    if [[ "$validation_passed" == "true" ]]; then
        log_success "All validation checks passed"
    else
        log_warn "Some validation checks failed - see above"
    fi
    
    return 0
}

show_post_update() {
    local script_dir
    script_dir=$(get_script_dir)
    local deploy_mode
    deploy_mode=$(get_deploy_mode "$script_dir")
    local compose_file
    compose_file=$(get_compose_filename "$script_dir")
    local backup_path="${1:-}"
    
    echo ""
    echo -e "${GREEN}${BOLD}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}${BOLD}║          LibraFoto Update Complete!                    ║${NC}"
    echo -e "${GREEN}${BOLD}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    echo -e "${BOLD}Version:${NC} $(get_current_version "$script_dir") ($(get_current_commit))"
    echo -e "${BOLD}Deploy Mode:${NC} $([ "$deploy_mode" == "ghcr" ] && echo "Pre-built GitHub images" || echo "Local source build")"
    echo ""
    
    echo -e "${BOLD}Container Status:${NC}"
    docker compose -f "$script_dir/docker/$compose_file" ps
    echo ""
    
    echo -e "${BOLD}Access URLs:${NC}"
    local ip_address
    ip_address=$(get_ip_address)
    echo "  Display UI: http://$ip_address/display/"
    echo "  Admin UI:   http://$ip_address/admin/"
    echo ""
    
    # Show validation results  
    if [[ -n "$backup_path" ]]; then
        validate_update "$script_dir" "$backup_path"
    fi
    
    echo -e "${BOLD}Log file:${NC} $LOG_FILE"
    echo ""
    
    # Cleanup old backups
    cleanup_old_backups
}

show_post_update_old() {
    local script_dir
    script_dir=$(get_script_dir)
    local deploy_mode
    deploy_mode=$(get_deploy_mode "$script_dir")
    local compose_file
    compose_file=$(get_compose_filename "$script_dir")
    
    echo ""
    echo -e "${GREEN}${BOLD}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}${BOLD}║              LibraFoto Update Complete!                    ║${NC}"
    echo -e "${GREEN}${BOLD}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    
    echo -e "${BOLD}Version:${NC} $(get_current_version "$script_dir") ($(get_current_commit))"
    echo -e "${BOLD}Deploy Mode:${NC} $([ "$deploy_mode" == "ghcr" ] && echo "Pre-built GitHub images" || echo "Local source build")"
    echo ""
    
    echo -e "${BOLD}Container Status:${NC}"
    docker compose -f "$script_dir/docker/$compose_file" ps
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
    
    # Parse arguments - keep only read-only flags
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
            --list-backups)
                list_backups
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                echo ""
                echo "Update is now fully interactive."
                echo "Run './update.sh' with no arguments for the interactive workflow."
                echo "Use '--help' for available options."
                exit 1
                ;;
        esac
    done
    
    # Initialize
    log_init "LibraFoto Update"
    show_update_banner
    
    # Check prerequisites
    check_prerequisites
    
    # Check for updates
    if ! check_for_updates; then
        exit 0
    fi
    
    # Exit if only checking
    if [[ "$check_only" == "true" ]]; then
        exit 0
    fi
    
    # Get context for preview
    local script_dir
    script_dir=$(get_script_dir)
    local deploy_mode
    deploy_mode=$(get_deploy_mode "$script_dir")
    
    # Count commits behind
    local commits_behind
    commits_behind=$(git rev-list --count HEAD..origin/main 2>/dev/null || git rev-list --count HEAD..origin/master 2>/dev/null || echo "?")
    
    # Show preview
    show_update_preview "$script_dir" "$deploy_mode" "$commits_behind"
    
    # Interactive prompts
    echo -e "${BOLD}Configuration Questions:${NC}\n"
    
    # Q1: Proceed with update?
    if ! confirm_prompt "Proceed with update?" "Y"; then
        echo "Update cancelled"
        exit 0
    fi
    
    # Q2: Stash changes? (only if local changes detected)
    cd "$script_dir"
    local force_stash="false"
    if ! git diff-index --quiet HEAD -- 2>/dev/null; then
        echo ""
        log_warn "Local changes detected in repository"
        if confirm_prompt "Stash changes and continue?" "Y"; then
            force_stash="true"
        else
            log_error "Update cancelled - please commit or stash your changes manually"
            exit 1
        fi
    fi
    
    # Q3: Switch deploy mode?
    echo ""
    local switch_mode=false
    local new_deploy_mode="$deploy_mode"
    
    echo -e "Current deploy mode: ${BOLD}$([ "$deploy_mode" == "ghcr" ] && echo "Pre-built GitHub images" || echo "Local source build")${NC}"
    if confirm_prompt "Switch deploy mode?" "N"; then
        if [[ "$deploy_mode" == "ghcr" ]]; then
            echo "Switching to: Local source build"
            new_deploy_mode="build"
        else
            echo "Switching to: Pre-built GitHub images"
            new_deploy_mode="ghcr"
        fi
        switch_mode=true
    fi
    
    # Q4: Use Docker build cache? (build mode only)
    echo ""
    local use_no_cache="false"
    if [[ "$new_deploy_mode" == "build" ]]; then
        if ! confirm_prompt "Use Docker build cache?" "Y"; then
            use_no_cache="true"
            log_info "Will rebuild with --no-cache"
        fi
    fi
    
    echo ""
    
    # Apply deploy mode switch if requested
    if [[ "$switch_mode" == "true" ]]; then
        set_env_var "DEPLOY_MODE" "$new_deploy_mode" "$script_dir"
        
        if [[ "$new_deploy_mode" == "ghcr" ]]; then
            local version
            version=$(get_current_version "$script_dir")
            local image_tag
            image_tag=$(get_image_tag_for_version "$version")
            set_env_var "LIBRAFOTO_IMAGE_TAG" "$image_tag" "$script_dir"
        fi
        
        log_success "Deploy mode switched to: $new_deploy_mode"
        echo ""
    fi
    
    # Track if we need to rollback on failure
    local backup_path=""
    local update_failed=false
    
    # Create backup
    backup_path=$(create_backup)
    
    # Pull updates
    if ! pull_updates "$force_stash"; then
        update_failed=true
    fi
    
    # Run migrations (if pull succeeded)
    if [[ "$update_failed" != "true" ]]; then
        run_migrations || true  # Migrations are non-fatal
    fi
    
    # Rebuild containers (or pull images in GHCR mode)
    if [[ "$update_failed" != "true" ]]; then
        if ! rebuild_containers "$use_no_cache"; then
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
                log_warn "Rollback skipped. Contact support or check logs for manual recovery steps."
                echo ""
                echo "Backup location: $backup_path"
            fi
        fi
        
        exit 1
    fi
    
    # Success
    show_post_update "$backup_path"
}

# Run main function
main "$@"

# Docker Deployment Guide

This guide covers building, deploying, and managing LibraFoto using Docker containers.

## Overview

LibraFoto uses a multi-container Docker setup with an nginx reverse proxy:

- **Proxy Container** (`librafoto-proxy`): Nginx reverse proxy routing all traffic (port 80)
- **API Container** (`librafoto-api`): .NET 10 backend with modular monolith architecture (internal)
- **Display UI Container** (`librafoto-display-ui`): Nginx serving the fullscreen slideshow (internal)
- **Admin UI Container** (`librafoto-admin-ui`): Nginx serving the Angular admin interface (internal)

All containers support **multi-architecture builds** for AMD64 and ARM64 platforms (including Raspberry Pi).

## Quick Start

### Raspberry Pi Installation (Recommended)

For Raspberry Pi deployments with kiosk mode, use the automated installation script:

```bash
git clone https://github.com/librafoto/librafoto
cd librafoto
sudo ./install.sh
```

The script automatically:

- Validates system requirements (Pi 4+, 64-bit OS, 2GB+ RAM)
- Installs Docker and Docker Compose
- Configures kiosk mode with Chromium fullscreen
- Builds and deploys all containers
- Displays access URLs and QR code for Admin UI

Options:

- `sudo ./install.sh --help` - Show all options
- `sudo ./install.sh --skip-kiosk` - Skip kiosk mode (headless server)
- `sudo ./install.sh --uninstall` - Remove LibraFoto

### Production Deployment (Manual)

```bash
# Set version (optional, defaults to 'dev')
export VERSION=$(cat .version)

# Build and start services
cd docker
docker compose up -d

# Check health status
docker compose ps

# View logs
docker compose logs -f
```

### Accessing the Application

All traffic goes through the nginx reverse proxy on port 80:

- **Root** (`/`): Redirects to Display UI
- **Display UI** (Fullscreen Slideshow): http://localhost/display/
- **Admin UI** (Management Interface): http://localhost/admin/
- **API**: http://localhost/api/

## Building Images

### Local Build (Single Architecture)

```bash
# Build for current platform
docker compose build

# Build with specific version
VERSION=1.0.0 docker compose build
```

### Multi-Architecture Build

Requires Docker Buildx:

```bash
# Enable BuildKit
export DOCKER_BUILDKIT=1

# Create buildx builder (one-time setup)
docker buildx create --name multiarch --use
docker buildx inspect --bootstrap

# Build API for multiple platforms
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg VERSION=$(cat .version) \
  -t librafoto-api:$(cat .version) \
  -f src/LibraFoto.Api/Dockerfile \
  .

# Build Frontend for multiple platforms
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg VERSION=$(cat .version) \
  -t librafoto-frontend:$(cat .version) \
  -f docker/Dockerfile.frontend \
  .
```

### Build with Registry Push

```bash
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --build-arg VERSION=$(cat .version) \
  -t ghcr.io/yourusername/librafoto-api:$(cat .version) \
  -t ghcr.io/yourusername/librafoto-api:latest \
  --push \
  -f src/LibraFoto.Api/Dockerfile \
  .
```

## CI/CD with GitHub Actions

The repository includes automated multi-arch builds via GitHub Actions (`.github/workflows/docker-build.yml`).

### Automatic Builds

- **On Push to Main**: Builds and pushes images tagged with `main` and `latest`
- **On Pull Request**: Builds images without pushing (validation only)
- **On Version Tag** (`v*.*.*`): Builds and pushes with semantic version tags

### Triggering a Release Build

```bash
# Update version
echo "1.2.0" > .version
git add .version
git commit -m "Release version 1.2.0"
git tag v1.2.0
git push origin main --tags
```

This creates images tagged as:

- `ghcr.io/owner/librafoto-api:1.2.0`
- `ghcr.io/owner/librafoto-api:1.2`
- `ghcr.io/owner/librafoto-api:1`
- `ghcr.io/owner/librafoto-api:latest`

### Using Pre-built Images

```yaml
# docker-compose.yml
services:
  api:
    image: ghcr.io/yourusername/librafoto-api:1.0.0
    # ... rest of configuration

  frontend:
    image: ghcr.io/yourusername/librafoto-frontend:1.0.0
    # ... rest of configuration
```

## Configuration

### Environment Variables

#### API Container

| Variable                      | Description                      | Default      | Required |
| ----------------------------- | -------------------------------- | ------------ | -------- |
| `ASPNETCORE_ENVIRONMENT`      | Runtime environment              | `Production` | No       |
| `LIBRAFOTO_DATA_DIR`          | Base data directory              | `/data`      | No       |
| `LIBRAFOTO_HOST_IP`           | Host machine IP for QR code URLs | Auto-detect  | No       |
| `Jwt__Key`                    | JWT signing key                  | -            | **Yes**  |
| `Jwt__Issuer`                 | JWT issuer                       | `LibraFoto`  | No       |
| `Jwt__Audience`               | JWT audience                     | `LibraFoto`  | No       |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OpenTelemetry endpoint           | -            | No       |

**Notes**:

- `LIBRAFOTO_DATA_DIR` automatically sets:
  - Database path: `${LIBRAFOTO_DATA_DIR}/librafoto.db`
  - Photo storage: `${LIBRAFOTO_DATA_DIR}/photos`
- `LIBRAFOTO_HOST_IP`: Override auto-detection of host IP for Display UI QR codes. The system automatically detects the correct IP from the Host header in most cases. Set this only if QR codes show incorrect addresses (e.g., custom networking scenarios).

### Volumes

```yaml
volumes:
  - librafoto-data:/data # Contains database and photos
```

**Important**: Back up this volume regularly!

### Resource Limits

Default limits (configured in `docker-compose.yml`):

| Service  | CPU | Memory | Reserved CPU | Reserved Memory |
| -------- | --- | ------ | ------------ | --------------- |
| API      | 1.0 | 1GB    | 0.5          | 512MB           |
| Frontend | 1.0 | 1GB    | 0.5          | 512MB           |

Adjust based on your workload:

```yaml
deploy:
  resources:
    limits:
      cpus: "2.0"
      memory: 2G
```

### Health Checks

Both services include health checks:

**API**: HTTP GET to `/health` endpoint

- Interval: 30s
- Timeout: 10s
- Retries: 3
- Start period: 40s

**Frontend**: HTTP GET to Nginx root

- Interval: 30s
- Timeout: 10s
- Retries: 3
- Start period: 10s

Check health status:

```bash
docker compose ps
# OR
docker inspect librafoto-api-1 --format='{{.State.Health.Status}}'
```

## Raspberry Pi Deployment

### Automated Installation (Recommended)

Use the installation script for a complete setup including kiosk mode:

```bash
git clone https://github.com/librafoto/librafoto
cd librafoto
sudo ./install.sh
```

See [Raspberry Pi Installation](#raspberry-pi-installation-recommended) above for details.

### Manual Installation

#### Requirements

- Raspberry Pi 4 or later (recommended)
- 64-bit Raspberry Pi OS
- Docker 20.10+ with BuildKit support
- At least 2GB RAM

#### Installation

```bash
# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Reboot or log out/in for group changes
sudo reboot

# Verify multi-arch support
docker buildx version
```

#### Manual Deployment

Same as standard deployment:

```bash
# Pull pre-built ARM64 images
docker compose pull

# Or build locally (slower)
docker compose build

# Start services
docker compose up -d
```

### Performance Considerations

- **Photo Processing**: First-time thumbnail generation may take longer on ARM
- **Database**: SQLite performs well on Raspberry Pi for small-medium libraries
- **Storage**: Use external USB 3.0 SSD for better I/O performance
- **Memory**: Enable swap if you have <2GB RAM

### Troubleshooting Raspberry Pi

**Issue: Out of memory during build**

```bash
# Build one service at a time
docker compose build api
docker compose build frontend
```

**Issue: Slow image processing**

```yaml
# Reduce concurrent operations in docker-compose.yml
deploy:
  resources:
    limits:
      cpus: "0.5" # Limit CPU to prevent overheating
```

**Issue: Container crashes**

```bash
# Check system resources
free -h
df -h
docker stats

# Increase swap
sudo dphys-swapfile swapoff
sudo nano /etc/dphys-swapfile  # Set CONF_SWAPSIZE=2048
sudo dphys-swapfile setup
sudo dphys-swapfile swapon
```

## Production Deployment Checklist

### Security

- [ ] Set strong `Jwt__Key` secret (minimum 32 characters)
- [ ] Use secrets management (Docker secrets, environment file)
- [ ] Configure firewall rules (expose only port 80/443)
- [ ] Enable HTTPS with reverse proxy (Traefik, Caddy, Nginx)
- [ ] Regularly update images for security patches
- [ ] Review and restrict container capabilities if needed

### Data Management

- [ ] Configure volume backup strategy
- [ ] Test restore procedure
- [ ] Set up database backup automation
- [ ] Monitor disk usage for photo storage
- [ ] Plan for storage scaling

### Monitoring

- [ ] Set up container monitoring (Prometheus, Grafana)
- [ ] Configure log aggregation
- [ ] Set up alerts for health check failures
- [ ] Monitor resource usage (CPU, memory, disk)
- [ ] Enable OpenTelemetry export if needed

### Operations

- [ ] Document deployment process
- [ ] Set up automated updates (Watchtower) or manual schedule
- [ ] Configure log rotation
- [ ] Plan for zero-downtime upgrades
- [ ] Test rollback procedure

## Backup and Restore

### Backup

```bash
# Stop containers (optional but recommended for consistency)
docker compose down

# Backup data volume
docker run --rm \
  -v librafoto-data:/data \
  -v $(pwd)/backups:/backup \
  alpine tar czf /backup/librafoto-backup-$(date +%Y%m%d).tar.gz /data

# Restart
docker compose up -d
```

### Restore

```bash
# Stop containers
docker compose down

# Restore from backup
docker run --rm \
  -v librafoto-data:/data \
  -v $(pwd)/backups:/backup \
  alpine sh -c "cd / && tar xzf /backup/librafoto-backup-YYYYMMDD.tar.gz"

# Start containers
docker compose up -d
```

### Automated Backup Script

```bash
#!/bin/bash
# backup-librafoto.sh

BACKUP_DIR="/path/to/backups"
RETENTION_DAYS=30

# Create backup
docker run --rm \
  -v librafoto-data:/data \
  -v ${BACKUP_DIR}:/backup \
  alpine tar czf /backup/librafoto-$(date +%Y%m%d-%H%M%S).tar.gz /data

# Remove old backups
find ${BACKUP_DIR} -name "librafoto-*.tar.gz" -mtime +${RETENTION_DAYS} -delete

echo "Backup completed: $(date)"
```

Add to crontab:

```bash
# Daily backup at 2 AM
0 2 * * * /path/to/backup-librafoto.sh >> /var/log/librafoto-backup.log 2>&1
```

## Updating

### Automated Update (Recommended)

Use the update script for safe updates with automatic backup and rollback:

```bash
# Check for available updates
sudo ./update.sh --check

# Apply updates interactively
sudo ./update.sh

# Force update without prompts
sudo ./update.sh --force

# Rollback to previous version
sudo ./update.sh --rollback
```

The update script:

- Creates a timestamped backup of your database and configuration
- Pulls the latest code from git
- Rebuilds Docker containers
- Runs health checks after deployment
- Automatically rolls back if health checks fail

### Manual Update

```bash
# Pull latest images
docker compose pull

# Recreate containers
docker compose up -d

# Verify health
docker compose ps
docker compose logs -f
```

### Automated Updates with Watchtower

```yaml
# Add to docker-compose.yml
services:
  watchtower:
    image: containrrr/watchtower
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - WATCHTOWER_CLEANUP=true
      - WATCHTOWER_POLL_INTERVAL=86400 # Check daily
    restart: unless-stopped
```

## Troubleshooting

### View Logs

```bash
# All services
docker compose logs -f

# Specific service
docker compose logs -f api

# Last 100 lines
docker compose logs --tail=100 api
```

### Check Health Status

```bash
docker compose ps
docker inspect librafoto-api-1 --format='{{json .State.Health}}' | jq
```

### Container Not Starting

```bash
# Check logs
docker compose logs api

# Common issues:
# - Missing JWT secret
# - Permission issues with volumes
# - Port conflicts
```

### Database Issues

```bash
# Access database
docker compose exec api sh
# Inside container:
ls -la /data
cat /data/librafoto.db

# Check permissions
# Should be owned by container user
```

### Frontend Not Loading

```bash
# Check nginx config
docker compose exec frontend cat /etc/nginx/nginx.conf

# Check frontend files
docker compose exec frontend ls -la /usr/share/nginx/html/

# Test API connectivity from frontend container
docker compose exec frontend wget -O- http://api:8080/health
```

### Reset Everything

```bash
# WARNING: This deletes all data!
docker compose down -v
docker compose up -d
```

## Advanced Configuration

### Custom Network

```yaml
networks:
  librafoto:
    driver: bridge
    ipam:
      config:
        - subnet: 172.20.0.0/16

services:
  api:
    networks:
      - librafoto
```

### External Reverse Proxy

Example with Traefik:

```yaml
services:
  api:
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.librafoto-api.rule=Host(`api.example.com`)"
      - "traefik.http.routers.librafoto-api.tls=true"
      - "traefik.http.services.librafoto-api.loadbalancer.server.port=8080"

  frontend:
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.librafoto.rule=Host(`photos.example.com`)"
      - "traefik.http.routers.librafoto.tls=true"
```

### Multiple Instances

```bash
# Create separate compose file
cp docker-compose.yml docker-compose.instance2.yml

# Modify ports and volume names in instance2.yml
# Start second instance
docker compose -f docker-compose.instance2.yml up -d
```

## Additional Resources

- [Aspire and Docker Documentation](../development/aspire-and-docker.md)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [Docker BuildKit](https://docs.docker.com/build/buildkit/)
- [Multi-platform Builds](https://docs.docker.com/build/building/multi-platform/)

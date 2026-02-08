# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

### Changed
- Renamed default branch to `main` and aligned CI/update checks with the new branch name

### Deprecated

### Removed

### Fixed

### Security

## [0.1.1] - 2026-02-08

### Changed

- Renamed default branch to `main` and aligned CI/update checks with the new branch name

## [0.1.0] - 2026-02-07

### Added
- Initial alpha release of LibraFoto
- Modular monolith architecture with Admin, Auth, Display, Media, and Storage modules
- Angular 21 admin frontend with Material Design
- Vanilla TypeScript slideshow display frontend optimized for Raspberry Pi
- Local storage provider with filesystem support
- Google Photos storage provider integration
- Thumbnail generation with ImageSharp (JPEG, PNG, HEIF/HEIC support)
- EXIF metadata extraction and GPS coordinate parsing
- Album and tag management
- JWT-based authentication with role-based access control
- Guest link sharing for albums
- Slideshow engine with configurable display settings
- Docker Compose deployment with automatic service orchestration
- Installation, update, and uninstall scripts for Linux/macOS
- Aspire-based development environment
- Comprehensive E2E tests with Playwright
- TUnit backend testing with 80% coverage requirement
- CI/CD with GitHub Actions for build validation and releases

#!/usr/bin/env bash
#
# suggest-version.sh
# Analyzes commits since last tag and suggests next version based on Conventional Commits
#
# Usage: bash scripts/suggest-version.sh

set -euo pipefail

# Colors for output
readonly COLOR_BLUE='\033[0;34m'
readonly COLOR_GREEN='\033[0;32m'
readonly COLOR_YELLOW='\033[1;33m'
readonly COLOR_RED='\033[0;31m'
readonly COLOR_CYAN='\033[0;36m'
readonly COLOR_RESET='\033[0m'

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Read current version from .version file
if [[ ! -f "$REPO_ROOT/.version" ]]; then
    echo -e "${COLOR_RED}Error: .version file not found at $REPO_ROOT/.version${COLOR_RESET}" >&2
    exit 1
fi

CURRENT_VERSION=$(cat "$REPO_ROOT/.version" | tr -d '\n' | tr -d '\r')
echo -e "${COLOR_CYAN}Current version: ${COLOR_RESET}$CURRENT_VERSION"
echo ""

# Find last git tag (stable or prerelease)
LAST_TAG=$(git -C "$REPO_ROOT" describe --tags --abbrev=0 2>/dev/null || echo "")

if [[ -z "$LAST_TAG" ]]; then
    # No tags yet, find first commit
    FIRST_COMMIT=$(git -C "$REPO_ROOT" rev-list --max-parents=0 HEAD)
    COMMIT_RANGE="$FIRST_COMMIT..HEAD"
    echo -e "${COLOR_YELLOW}No git tags found. Analyzing all commits since first commit.${COLOR_RESET}"
else
    COMMIT_RANGE="$LAST_TAG..HEAD"
    echo -e "${COLOR_CYAN}Last tag: ${COLOR_RESET}$LAST_TAG"
fi

echo -e "${COLOR_CYAN}Analyzing commits: ${COLOR_RESET}$COMMIT_RANGE"
echo ""

# Analyze commits for conventional commit types
BREAKING_CHANGES=0
FEAT_COUNT=0
FIX_COUNT=0
OTHER_COUNT=0

while IFS= read -r commit_msg; do
    # Check for breaking changes (! in type or BREAKING CHANGE in body)
    if [[ "$commit_msg" =~ ^[a-z]+(\([a-z0-9-]+\))?!: ]] || [[ "$commit_msg" =~ BREAKING[[:space:]]CHANGE ]]; then
        ((BREAKING_CHANGES++))
    # Check for feat commits
    elif [[ "$commit_msg" =~ ^feat(\([a-z0-9-]+\))?: ]]; then
        ((FEAT_COUNT++))
    # Check for fix commits
    elif [[ "$commit_msg" =~ ^fix(\([a-z0-9-]+\))?: ]]; then
        ((FIX_COUNT++))
    else
        ((OTHER_COUNT++))
    fi
done < <(git -C "$REPO_ROOT" log --pretty=format:"%s" "$COMMIT_RANGE" 2>/dev/null)

TOTAL_COMMITS=$((BREAKING_CHANGES + FEAT_COUNT + FIX_COUNT + OTHER_COUNT))

echo -e "${COLOR_BLUE}📊 Commit Analysis:${COLOR_RESET}"
echo "  Total commits: $TOTAL_COMMITS"
echo "  Breaking changes: $BREAKING_CHANGES"
echo "  Features (feat): $FEAT_COUNT"
echo "  Bug fixes (fix): $FIX_COUNT"
echo "  Other (docs, chore, etc.): $OTHER_COUNT"
echo ""

# Parse current version into components
# Handle both stable (1.2.3) and prerelease (1.2.3-alpha.1) formats
if [[ "$CURRENT_VERSION" =~ ^([0-9]+)\.([0-9]+)\.([0-9]+)(-([a-z]+)\.([0-9]+))?$ ]]; then
    MAJOR="${BASH_REMATCH[1]}"
    MINOR="${BASH_REMATCH[2]}"
    PATCH="${BASH_REMATCH[3]}"
    PRERELEASE_TYPE="${BASH_REMATCH[5]:-}"  # alpha, beta, rc, or empty
    PRERELEASE_NUM="${BASH_REMATCH[6]:-}"   # prerelease number, or empty
else
    echo -e "${COLOR_RED}Error: Invalid version format in .version: $CURRENT_VERSION${COLOR_RESET}" >&2
    echo "Expected format: #.#.# or #.#.#-alpha.# or #.#.#-beta.# or #.#.#-rc.#" >&2
    exit 1
fi

# Determine suggested version based on semantic versioning rules
SUGGESTED_VERSION=""
BUMP_TYPE=""
REASONING=""

if [[ $TOTAL_COMMITS -eq 0 ]]; then
    SUGGESTED_VERSION="$CURRENT_VERSION"
    BUMP_TYPE="none"
    REASONING="No new commits since last tag"
elif [[ $BREAKING_CHANGES -gt 0 ]]; then
    # Breaking changes → major version bump
    BUMP_TYPE="major"
    NEW_MAJOR=$((MAJOR + 1))
    NEW_MINOR=0
    NEW_PATCH=0
    REASONING="Found $BREAKING_CHANGES breaking change(s) → major version bump"
    
    if [[ -n "$PRERELEASE_TYPE" ]]; then
        SUGGESTED_VERSION="$NEW_MAJOR.$NEW_MINOR.$NEW_PATCH-$PRERELEASE_TYPE.1"
    else
        SUGGESTED_VERSION="$NEW_MAJOR.$NEW_MINOR.$NEW_PATCH"
    fi
elif [[ $FEAT_COUNT -gt 0 ]]; then
    # Features → minor version bump
    BUMP_TYPE="minor"
    NEW_MINOR=$((MINOR + 1))
    NEW_PATCH=0
    REASONING="Found $FEAT_COUNT feature(s) → minor version bump"
    
    if [[ -n "$PRERELEASE_TYPE" ]]; then
        SUGGESTED_VERSION="$MAJOR.$NEW_MINOR.$NEW_PATCH-$PRERELEASE_TYPE.1"
    else
        SUGGESTED_VERSION="$MAJOR.$NEW_MINOR.$NEW_PATCH"
    fi
elif [[ $FIX_COUNT -gt 0 ]]; then
    # Bug fixes → patch version bump
    BUMP_TYPE="patch"
    NEW_PATCH=$((PATCH + 1))
    REASONING="Found $FIX_COUNT bug fix(es) → patch version bump"
    
    if [[ -n "$PRERELEASE_TYPE" ]]; then
        SUGGESTED_VERSION="$MAJOR.$MINOR.$NEW_PATCH-$PRERELEASE_TYPE.1"
    else
        SUGGESTED_VERSION="$MAJOR.$MINOR.$NEW_PATCH"
    fi
else
    # Only chore/docs/etc → no version change or prerelease bump
    BUMP_TYPE="prerelease"
    REASONING="Only non-functional changes (docs, chore, etc.) → bump prerelease if applicable"
    
    if [[ -n "$PRERELEASE_TYPE" ]]; then
        NEW_PRERELEASE_NUM=$((PRERELEASE_NUM + 1))
        SUGGESTED_VERSION="$MAJOR.$MINOR.$PATCH-$PRERELEASE_TYPE.$NEW_PRERELEASE_NUM"
    else
        SUGGESTED_VERSION="$CURRENT_VERSION"
        REASONING="Only non-functional changes → no version bump needed for stable release"
    fi
fi

echo -e "${COLOR_GREEN}✨ Suggested Next Version:${COLOR_RESET}"
echo -e "  ${COLOR_GREEN}$SUGGESTED_VERSION${COLOR_RESET}"
echo ""
echo -e "${COLOR_BLUE}Reasoning:${COLOR_RESET}"
echo "  $REASONING"
echo "  Bump type: $BUMP_TYPE"
echo ""

# Show command to update version
if [[ "$SUGGESTED_VERSION" != "$CURRENT_VERSION" ]]; then
    echo -e "${COLOR_CYAN}To update version:${COLOR_RESET}"
    echo "  echo \"$SUGGESTED_VERSION\" > .version"
    echo "  git add .version && git commit -m \"chore: bump version to $SUGGESTED_VERSION\""
    echo ""
fi

# Show recent commits for context
echo -e "${COLOR_BLUE}📝 Recent Commits:${COLOR_RESET}"
git -C "$REPO_ROOT" log --pretty=format:"  %C(yellow)%h%C(reset) %s %C(dim)(%cr)%C(reset)" "$COMMIT_RANGE" | head -n 10

# If more than 10 commits, show count
if [[ $TOTAL_COMMITS -gt 10 ]]; then
    echo ""
    echo -e "${COLOR_CYAN}  ... and $((TOTAL_COMMITS - 10)) more${COLOR_RESET}"
fi

echo ""

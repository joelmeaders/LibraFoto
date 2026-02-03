#!/usr/bin/env bash

set -o pipefail

THIS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$THIS_DIR/../.." && pwd)"

find_shunit2() {
    if [[ -n "${SHUNIT2_PATH:-}" && -f "${SHUNIT2_PATH}" ]]; then
        echo "$SHUNIT2_PATH"
        return 0
    fi

    if command -v shunit2 &>/dev/null; then
        command -v shunit2
        return 0
    fi

    local candidates=(
        "/usr/share/shunit2/shunit2"
        "/usr/local/share/shunit2/shunit2"
        "/opt/homebrew/Cellar/shunit2" 
    )

    for candidate in "${candidates[@]}"; do
        if [[ -f "$candidate" ]]; then
            echo "$candidate"
            return 0
        fi
        if [[ -d "$candidate" ]]; then
            local match
            match=$(find "$candidate" -type f -name shunit2 2>/dev/null | head -n 1)
            if [[ -n "$match" ]]; then
                echo "$match"
                return 0
            fi
        fi
    done

    return 1
}

make_temp_dir() {
    local dir
    dir=$(mktemp -d 2>/dev/null || mktemp -d -t librafoto-shell-tests)
    echo "$dir"
}

create_mock_command() {
    local bin_dir="$1"
    local name="$2"
    local content="$3"

    mkdir -p "$bin_dir"
    cat > "$bin_dir/$name" << EOF
#!/usr/bin/env bash
$content
EOF
    chmod +x "$bin_dir/$name"
}

assertFileContains() {
    local file="$1"
    local expected="$2"
    grep -q "$expected" "$file"
    assertTrue "Expected '$file' to contain '$expected'" $?
}

assertFileExists() {
    local file="$1"
    assertTrue "Expected '$file' to exist" "[[ -f \"$file\" ]]"
}

#!/usr/bin/env sh

CLI='[CLI]'
TMP='backups/tmp'
ROOT_DIR="$(pwd -P)"
EXE="$ROOT_DIR/cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe"

ANSI_RESET=$(tput setaf 15)
ANSI_BOLD=$(tput bold)
ANSI_RED=$(tput setaf 1)
ANSI_GREEN=$(tput setaf 2)
ANSI_YELLOW=$(tput setaf 3)

PASS=0
FAIL=0
SKIP=0
FAILED_TESTS=()
FAILED_MSG=()

abspath() {
    local p="$1"
    [ -e "$p" ] || mkdir -p "$p" 2>/dev/null || true
    if command -v cygpath > /dev/null 2>&1; then
        cygpath -m "$p"
    elif command -v wslpath > /dev/null 2>&1; then
        # WSL — converts /mnt/c/... to C:/...
        wslpath -m "$p"
    else
        (cd "$(dirname "$p")" && printf '%s/%s\n' "$(pwd -P)" "$(basename "$p")")
    fi
}

crlf-lf-normalize() {
  s=${1//$'\r\n'/$'\n'} # change CRLFs to LFs
  s=${1%$'\n'}          # eliminate any trailing LF
  #sed -E <<< "$s"
}

# Run independently
if [ ! -x "$EXE" ] && [ ! -f "$EXE" ]; then make all; fi

# Sandbox where the CLI runs (state.json + save.log land here)
RUN_DIR="$ROOT_DIR/tests/run"
FIX_DIR="$RUN_DIR/fixtures"
rm -rf "$RUN_DIR"
mkdir -p "$FIX_DIR"

# Build a fresh source tree (will be reused/restored across tests)
SRC1="$FIX_DIR/source1"
SRC2="$FIX_DIR/source2"
DST1="$FIX_DIR/destination1"
DST2="$FIX_DIR/destination2"
mkdir -p "$SRC1/subdirectory" "$SRC2"
echo "alpha"  > "$SRC1/file1.txt"
echo "beta"   > "$SRC1/file2.txt"
echo "gamma"  > "$SRC1/subdirectory/deep.txt"
echo "delta"  > "$SRC2/data.txt"

SRC1_ABS="$(abspath "$SRC1")"
SRC2_ABS="$(abspath "$SRC2")"
DST1_ABS="$(abspath "$DST1")"
DST2_ABS="$(abspath "$DST2")"

# Pre-populate state.json so the CLI doesn't prompt for save info
seed_state() {
    cat > "$RUN_DIR/state.json" << EOF
[
  {
    "Id": "00000000-0000-0000-0000-000000000001",
    "Name": "save1",
    "SourcePath": "$SRC1_ABS",
    "DestinationPath": "$DST1_ABS",
    "LastActionTime": "2025-01-01T00:00:00",
    "Status": "Inactive"
  },
  {
    "Id": "00000000-0000-0000-0000-000000000002",
    "Name": "save2",
    "SourcePath": "$SRC2_ABS",
    "DestinationPath": "$DST2_ABS",
    "LastActionTime": "2025-01-01T00:00:00",
    "Status": "Inactive"
  }
]
EOF
}

reset_destinations() {
    rm -rf "$DST1" "$DST2"
}

# Run the CLI from inside RUN_DIR with stdin closed so prompts don't hang
run_cli() {
    (cd "$RUN_DIR" && "$EXE" "$@" < /dev/null > /tmp/easysave_out 2>&1)
    return $?
}

pass() {
    echo -e "├${ANSI_GREEN}[PASS]${ANSI_RESET} ${CLI} $1"
    PASS=$((PASS + 1))
}

fail() {
    echo -e "├${ANSI_RED}[FAIL]${ANSI_RESET} ${CLI} $1"
    if [ -n "${2:-}" ]; then FAILED_MSG+=("$2\n${ANSI_YELLOW}$3${ANSI_RESET}\n$4\n${ANSI_YELLOW}$5${ANSI_RESET}")
    else FAILED_MSG+=(""); fi
    FAIL=$((FAIL + 1))
    FAILED_TESTS+=("${CLI} $1")
}

skip() {
    echo -e "├${ANSI_YELLOW}[SKIP]${ANSI_RESET} ${CLI} $2"
    SKIP=$((SKIP + 1))
}

assert_eq() {
    local label="$1" content="$2" expected="$3"
    if [ "$(crlf-lf-normalize "$content")" = "$(crlf-lf-normalize "$expected")" ]; then pass "$label";
    else fail "$label" "| EXPECTED |" "$expected" "| GOT |" "$content";
    fi
}

assert_contains() {
    local label="$1" content="$2" needle="$3"
    if printf '%s' "$content" | grep -q -- "$needle"; then pass "$label";
    else fail "$label" "| EXPECTED TO CONTAIN |" "$needle" "| GOT |" "$content";
    fi
}

assert_file_exists() {
    local label="$1" path="$2"
    if [ -f "$path" ]; then pass "$label";
    else fail "$label" "| MISSING FILE |" "$path" "" "";
    fi
}

assert_file_nonempty() {
    local label="$1" path="$2"
    if [ -s "$path" ]; then pass "$label";
    else fail "$label" "| MISSING OR EMPTY |" "$path" "" "";
    fi
}

assert_dirs_match() {
    local label="$1" a="$2" b="$3"
    # .diff sidecars in the destination are restore metadata, not user data —
    # exclude them when comparing against the source tree.
    if diff -r --exclude='*.diff' "$a" "$b" > /dev/null 2>&1; then pass "$label";
    else fail "$label" "| DIRECTORIES DIFFER |" "$a\n$b";
    fi
}

##################################################################
##                            TESTS                             ##
##################################################################

cat << EOF

╔════════════════════════════════════════════════╗
║                    CLI TEST                    ║
╚════════════════════════════════════════════════╝

EOF

cat << EOF
╒═ Help ═════════════════════════════════════════╕
EOF

HELP_MSG=$(cat << EOF
Usage: EasySave.exe [OPTIONS] [ARGUMENTS]\r
\r
OPTIONS:\r
      --save      Save (default)\r
  -h, --help      Display this help message\r
  -v, --version   Version\r
\r
ARGUMENTS:\r
  N               One single save (from 1 to 5 included)\r
  N-M             Range of saves (from N to M)\r
  N;M             Multiple saves (N and M)\r
EOF
)

run_cli --help
assert_eq "--help" "$(cat /tmp/easysave_out)" "$HELP_MSG"
run_cli -h
assert_eq "-h" "$(cat /tmp/easysave_out)" "$HELP_MSG"

cat << EOF
╞═ Version ══════════════════════════════════════╡
EOF

VERSION='EasySave v1.1'
run_cli --version
assert_eq "--version" "$(cat /tmp/easysave_out)" "$VERSION"
run_cli -v
assert_eq "-v" "$(cat /tmp/easysave_out)" "$VERSION"


cat << EOF
╞═ Complete save - single ═══════════════════════╡
EOF

seed_state
reset_destinations
run_cli 1
assert_eq "1: no output" "$(cat /tmp/easysave_out)" ""
assert_file_exists "1: file1.txt" "$DST1/file1.txt"
assert_file_exists "1: file2.txt" "$DST1/file2.txt"
assert_file_exists "1: subdirectory/deep.txt" "$DST1/subdirectory/deep.txt"
assert_dirs_match  "1: source1/ == destination1/" "$SRC1" "$DST1"

run_cli --complete 1
assert_eq "--complete 1: no output" "$(cat /tmp/easysave_out)" ""
assert_dirs_match  "--complete 1: source1/ == destination1/" "$SRC1" "$DST1"

cat << EOF
╞═ Complete save - range ════════════════════════╡
EOF

seed_state
reset_destinations
run_cli 1-2
assert_eq "1-2: no output" "$(cat /tmp/easysave_out)" ""
assert_dirs_match "1-2: destination1/" "$SRC1" "$DST1"
assert_dirs_match "1-2: destination2/" "$SRC2" "$DST2"

run_cli --complete 1-2
assert_eq "--complete 1-2: no output" "$(cat /tmp/easysave_out)" ""
assert_dirs_match "--complete 1-2: destination1/" "$SRC1" "$DST1"
assert_dirs_match "--complete 1-2: destination2/" "$SRC2" "$DST2"

cat << EOF
╞═ Complete save - sequence ═════════════════════╡
EOF

seed_state
reset_destinations
run_cli "1;2"
assert_eq "1;2: no output" "$(cat /tmp/easysave_out)" ""
assert_dirs_match "1;2: destination1/" "$SRC1" "$DST1"
assert_dirs_match "1;2: destination2/" "$SRC2" "$DST2"

run_cli --complete "1;2"
assert_eq "--complete 1;2: no output" "$(cat /tmp/easysave_out)" ""
assert_dirs_match "--complete 1;2: destination1/" "$SRC1" "$DST1"
assert_dirs_match "--complete 1;2: destination2/" "$SRC2" "$DST2"

cat << EOF
╞═ Differential save - noop ═════════════════════╡
EOF

seed_state
reset_destinations
run_cli 1
ts_before="$(stat -c %Y "$DST1/file1.txt" 2>/dev/null || stat -f %m "$DST1/file1.txt")"
sleep 1
run_cli --differential 1
assert_eq "--differential 1: no output" "$(cat /tmp/easysave_out)" ""
ts_after="$(stat -c %Y "$DST1/file1.txt" 2>/dev/null || stat -f %m "$DST1/file1.txt")"
if [ "$ts_before" = "$ts_after" ]; then pass "--differential 1: noop (mtime preserved)"
else fail "--differential 1: file was re-copied even though unchanged"; fi
assert_dirs_match "--differential 1: source1/ == destination1/" "$SRC1" "$DST1"

cat << EOF
╞═ Differential save - single ═══════════════════╡
EOF

echo "alpha-modified" > "$SRC1/file1.txt"
run_cli --differential 1
assert_eq "--differential 1: no output" "$(cat /tmp/easysave_out)" ""
content="$(cat "$DST1/file1.txt")"
if [ "$content" = "alpha-modified" ]; then pass "--differential 1: modified file content was updated in dest"
else fail "--differential 1: destination still has stale content" "| EXPECTED |" "alpha-modified" "| GOT |" "$content"; fi
assert_dirs_match "--differential 1: destination1/ fully resynced with source1/" "$SRC1" "$DST1"

cat << EOF
╞═ Differential save - new file ═════════════════╡
EOF

echo "fresh" > "$SRC1/new_file.txt"
run_cli --differential 1
assert_eq "--differential 1: no output" "$(cat /tmp/easysave_out)" ""
assert_file_exists "--differential 1: new file was copied to destination1/" "$DST1/new_file.txt"

cat << EOF
╞═ State and log files ══════════════════════════╡
EOF

assert_file_nonempty "state.json was written" "$RUN_DIR/state.json"
assert_file_nonempty "save.log was written"   "$RUN_DIR/save.log"
first_char="$(head -c 1 "$RUN_DIR/state.json")"
if [ "$first_char" = "[" ] || [ "$first_char" = "{" ]; then pass "state.json starts with valid JSON delimiter"
else fail "state.json doesn't look like JSON"; fi


##################################################################
##                           SUM UP                             ##
##################################################################

echo -e "╘${ANSI_BOLD}═╡ ${ANSI_GREEN}Passed: $PASS${ANSI_RESET}${ANSI_BOLD} │ ${ANSI_RED}Failed: $FAIL${ANSI_RESET}${ANSI_BOLD} │ ${ANSI_YELLOW}Skipped: $SKIP${ANSI_RESET}${ANSI_BOLD} ╞════════${ANSI_RESET}╛"

if [ $FAIL -gt 0 ]; then
    echo -e "\n${ANSI_RED}FAILED${ANSI_RESET}\n"
    for (( i=0; i < ${#FAILED_TESTS[@]}; i++)); do
        echo -e "=> ${FAILED_TESTS[$i]}\n${FAILED_MSG[$i]}"
    done
    exit 1
fi

exit 0

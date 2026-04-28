#!/usr/bin/env sh

TMP=backups/tmp

ANSI_RESET=$(tput setaf 15)
ANSI_RED=$(tput setaf 1)
ANSI_GREEN=$(tput setaf 2)
ANSI_YELLOW=$(tput setaf 3)
EXE='cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe'

PASS=0
FAIL=0
FAILED_TESTS=()

# ---------- Setup -----------------------------------------------------------

# Resolve absolute Windows paths in a portable way.
# The CLI is a Windows .exe so paths in state.json must be in Windows form.
abspath() {
    local p="$1"
    # Make sure the path exists so cygpath/wslpath can resolve it
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

CLI_DIR="$(pwd -P)"
if [ ! -x "$CLI_DIR/$EXE" ] && [ ! -f "$CLI_DIR/$EXE" ]; then
    echo -e "${ANSI_RED}Cannot find $EXE — build the CLI in Release first:${ANSI_RESET}"
    echo "  dotnet build EasySave.CLI.csproj --configuration Release"
    exit 2
fi
# Keep the EXE path in bash-native form so WSL/Git Bash can launch it.
# Only the paths written into state.json need to be in Windows form.
EXE="$CLI_DIR/$EXE"

# Sandbox where the CLI runs (state.json + save.log land here)
RUN_DIR="$CLI_DIR/tests/run"
FIX_DIR="$RUN_DIR/fixtures"
rm -rf "$RUN_DIR"
mkdir -p "$FIX_DIR"

# Build a fresh source tree (will be reused/restored across tests)
SRC1="$FIX_DIR/src1"
SRC2="$FIX_DIR/src2"
DST1="$FIX_DIR/dst1"
DST2="$FIX_DIR/dst2"
mkdir -p "$SRC1/subdir" "$SRC2"
echo "alpha"  > "$SRC1/file1.txt"
echo "beta"   > "$SRC1/file2.txt"
echo "gamma"  > "$SRC1/subdir/deep.txt"
echo "delta"  > "$SRC2/data.txt"

SRC1_ABS="$(abspath "$SRC1")"
SRC2_ABS="$(abspath "$SRC2")"
DST1_ABS="$(abspath "$DST1")"
DST2_ABS="$(abspath "$DST2")"

# Pre-populate state.json so the CLI doesn't prompt for save info
seed_state() {
    cat > "$RUN_DIR/state.json" <<EOF
[
  {
    "Id": 1,
    "Name": "save1",
    "SourcePath": "$SRC1_ABS",
    "DestinationPath": "$DST1_ABS",
    "LastActionTime": "2025-01-01T00:00:00",
    "Status": "Inactive"
  },
  {
    "Id": 2,
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

# ---------- Assertions ------------------------------------------------------

pass() {
    echo -e "${ANSI_GREEN}[PASS]${ANSI_RESET} $1"
    PASS=$((PASS + 1))
}

fail() {
    echo -e "${ANSI_RED}[FAIL]${ANSI_RESET} $1"
    [ -n "${2:-}" ] && echo -e "       ${ANSI_YELLOW}$2${ANSI_RESET}"
    FAIL=$((FAIL + 1))
    FAILED_TESTS+=("$1")
}

assert_contains() {
    local label="$1" content="$2" needle="$3"
    if printf '%s' "$content" | grep -q -- "$needle"; then pass "$label"
    else fail "$label" "expected to find: $needle"
    fi
}

assert_file_exists() {
    local label="$1" path="$2"
    if [ -f "$path" ]; then pass "$label"
    else fail "$label" "missing file: $path"
    fi
}

assert_file_nonempty() {
    local label="$1" path="$2"
    if [ -s "$path" ]; then pass "$label"
    else fail "$label" "empty or missing: $path"
    fi
}

assert_dirs_match() {
    local label="$1" a="$2" b="$3"
    if diff -r "$a" "$b" > /dev/null 2>&1; then pass "$label"
    else fail "$label" "directories differ: $a vs $b"
    fi
}

# ---------- Tests -----------------------------------------------------------

echo "=== Help / version ==="
run_cli --help
out="$(cat /tmp/easysave_out)"
assert_contains "--help shows usage banner"  "$out" "Usage: EasySave"
assert_contains "--help lists --version"     "$out" "version"

run_cli -h
assert_contains "-h shows usage banner"      "$(cat /tmp/easysave_out)" "Usage: EasySave"

run_cli --version
assert_contains "--version prints version"   "$(cat /tmp/easysave_out)" "EasySave v"

run_cli -v
assert_contains "-v prints version"          "$(cat /tmp/easysave_out)" "EasySave v"


echo
echo "=== Single complete save ==="
seed_state
reset_destinations
run_cli 1
assert_file_exists "save1: file1.txt copied"      "$DST1/file1.txt"
assert_file_exists "save1: file2.txt copied"      "$DST1/file2.txt"
assert_file_exists "save1: subdir/deep.txt copied" "$DST1/subdir/deep.txt"
assert_dirs_match  "save1: src1 == dst1"          "$SRC1" "$DST1"


echo
echo "=== Multi-save range '1-2' ==="
seed_state
reset_destinations
run_cli 1-2
assert_dirs_match "range 1-2 fills dst1" "$SRC1" "$DST1"
assert_dirs_match "range 1-2 fills dst2" "$SRC2" "$DST2"


echo
echo "=== Multi-save sequence '1;2' ==="
seed_state
reset_destinations
run_cli "1;2"
assert_dirs_match "sequence 1;2 fills dst1" "$SRC1" "$DST1"
assert_dirs_match "sequence 1;2 fills dst2" "$SRC2" "$DST2"


echo
echo "=== Differential save: unchanged source preserves dest ==="
seed_state
reset_destinations
run_cli 1                       # initial complete save
assert_dirs_match "diff: initial complete OK" "$SRC1" "$DST1"
ts_before="$(stat -c %Y "$DST1/file1.txt" 2>/dev/null || stat -f %m "$DST1/file1.txt")"
sleep 1
run_cli -t differential 1       # same files, must skip copy
ts_after="$(stat -c %Y "$DST1/file1.txt" 2>/dev/null || stat -f %m "$DST1/file1.txt")"
if [ "$ts_before" = "$ts_after" ]; then
    pass "diff: unchanged file was not re-copied (mtime preserved)"
else
    fail "diff: file was re-copied even though unchanged"
fi
assert_dirs_match "diff: dst1 still equals src1" "$SRC1" "$DST1"


echo
echo "=== Differential save: modified file produces .diff sidecar ==="
echo "alpha-modified" > "$SRC1/file1.txt"
run_cli -t differential 1
content="$(cat "$DST1/file1.txt")"
if [ "$content" = "alpha" ]; then
    pass "diff: dest file kept its previous content"
else
    fail "diff: dest was overwritten instead of producing a delta" "got: $content"
fi
assert_file_nonempty "diff: .diff sidecar was created" "$DST1/file1.txt.diff"


echo
echo "=== Differential save: new file in source is copied ==="
echo "fresh" > "$SRC1/new_file.txt"
run_cli -t differential 1
assert_file_exists "diff: new file was copied to dest" "$DST1/new_file.txt"


echo
echo "=== State and log files ==="
assert_file_nonempty "state.json was written" "$RUN_DIR/state.json"
assert_file_nonempty "save.log was written"   "$RUN_DIR/save.log"
# Crude JSON sanity check: must start with [ or {
first_char="$(head -c 1 "$RUN_DIR/state.json")"
if [ "$first_char" = "[" ] || [ "$first_char" = "{" ]; then
    pass "state.json starts with valid JSON delimiter"
else
    fail "state.json doesn't look like JSON"
fi


echo
echo "=== --type alias forms ==="
seed_state
reset_destinations
run_cli --type=differential 1
assert_dirs_match "--type=differential works" "$SRC1" "$DST1"

seed_state
reset_destinations
run_cli --type diff 1
assert_dirs_match "--type diff alias works" "$SRC1" "$DST1"


# ---------- Summary ---------------------------------------------------------

echo
echo "=========================================="
echo -e "${ANSI_GREEN}Passed: $PASS${ANSI_RESET}"
if [ $FAIL -gt 0 ]; then
    echo -e "${ANSI_RED}Failed: $FAIL${ANSI_RESET}"
    for t in "${FAILED_TESTS[@]}"; do
        echo "  - $t"
    done
    exit 1
fi
echo -e "${ANSI_GREEN}All tests passed.${ANSI_RESET}"
exit 0

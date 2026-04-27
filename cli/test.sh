#! /bin/env sh

TMP=backups/tmp

ANSI_RED='\x1b[31m'
ANSI_GREEN='\x1b[32m'
ANSI_RESET='\x1b[00m'

test() {
    if [ "$2" -eq "$3" ]; then echo -e "$1 | $ANSI_GREEN[OK]$ANSI_RESET"; else echo -e "$1 | $ANSI_RED-|KO|-$ANSI_RESET"; fi
}

## VERSION

version='backups/version'
cli_out_version='backups/cli-version'
diff_out='backups/diff'

cat << EOF > $version
EasySave v1.0.0
EOF
cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe --version > $cli_out_version
diff --strip-trailing-cr $cli_out_version $version > $diff_out
test "EasySave.CLI.exe --version" "$?" 0
cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe -v > $cli_out_version
diff --strip-trailing-cr $cli_out_version $version > $diff_out
test "EasySave.CLI.exe -v" "$?" 0

## HELP MESSAGE

help_message='backups/help_message'
cli_out_help='backups/cli-help'
diff_out='backups/diff'

cat << EOF > $help_message
Usage: EasySave.exe [OPTIONS] [ARGUMENTS]

OPTIONS:
      --save      Save (default)
  -h, --help      Display this help message
  -v, --version   Version

ARGUMENTS:
  N               One single save (from 1 to 5 included)
  N-M             Range of saves (from N to M)
  N;M             Multiple saves (N and M)
EOF
cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe --help > $cli_out_help
diff --strip-trailing-cr $cli_out_help $help_message > $diff_out
test "EasySave.CLI.exe --help" "$?" 0
cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe --help > $cli_out_help
diff --strip-trailing-cr $cli_out_help $help_message > $diff_out
test "EasySave.CLI.exe -h" "$?" 0
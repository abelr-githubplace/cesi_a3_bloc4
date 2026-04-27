#! /bin/env bash

ANSI_RED='\033[31m'
ANSI_GREEN='\033[32m'
ANSI_RESET='\033[00m'

test() {
    if [ "$2" -eq "$3" ]; then echo "$1 - \033[00mOK\033[00m"; else echo "$1 - KO"; fi
}

help_message='Usage: EasySave.exe [OPTIONS] [ARGUMENTS]\n\nOPTIONS:\n      --save      Save (default)\n  -h, --help      Display this help message\n  -v, --version   Version\n\nARGUMENTS:\n  N               One single save (from 1 to 5 included)\n  N-M             Range of saves (from N to M)\n  N;M             Multiple saves (N and M)'

bin/Release/net10.0/EasySave.CLI.exe --help > ../backups/cli-help
diff ../backup/cli-help 
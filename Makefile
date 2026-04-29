BUILD=dotnet build -verbosity q
TEST=dotnet test -verbosity q

DEBUG= --configuration Debug
RELEASE= --configuration Release
WIN_OS= --os win

DEBUG_BUILD=${BUILD} ${DEBUG} ${WIN_OS}
RELEASE_BUILD=${BUILD} ${RELEASE} ${WIN_OS}

DEBUG_LOGGER_PATH=./logger/bin/Debug/net10.0/win-x64/EasyLog.dll
DEBUG_LIBRARY_PATH=./lib/bin/Debug/net10.0/win-x64/EasySaveLibrary.dll
DEBUG_CLI_PATH=./cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe
DEBUG_GUI_PATH=./gui/bin/Debug/net10.0/win-x64/EasySave.GUI.exe
DEBUG_SERVER_PATH=./server/bin/Debug/net10.0/win-x64/EasySave.Server.exe
DEBUG_REMOTE_PATH=./remote/bin/Debug/net10.0/win-x64/EasySave.Remote.exe

LOGGER_PATH=./logger/bin/Release/net10.0/win-x64/EasyLog.dll
LIBRARY_PATH=./lib/bin/Release/net10.0/win-x64/EasySaveLibrary.dll
CLI_PATH=./cli/bin/Release/net10.0/win-x64/EasySave.CLI.exe
GUI_PATH=./gui/bin/Release/net10.0/win-x64/EasySave.GUI.exe
SERVER_PATH=./server/bin/Release/net10.0/win-x64/EasySave.Server.exe
REMOTE_PATH=./remote/bin/Release/net10.0/win-x64/EasySave.Remote.exe

all: ${DEBUG_CLI_PATH} ${DEBUG_GUI_PATH}

all-release: ${CLI_PATH} ${GUI_PATH}

# Logger

logger: ${DEBUG_LOGGER_PATH}

logger-release: ${LOGGER_PATH}

${DEBUG_LOGGER_PATH}:
	@-cd logger/ && ${DEBUG_BUILD} EasyLog.csproj

${LOGGER_PATH}:
	@-cd logger/ && ${RELEASE_BUILD} EasyLog.csproj

# Library

lib: ${DEBUG_LIBRARY_PATH}

lib-release: ${LIBRARY_PATH}

${DEBUG_LIBRARY_PATH}:
	@-cd lib/ && ${DEBUG_BUILD} EasySaveLibrary.csproj
		
${LIBRARY_PATH}:
	@-cd lib/ && ${RELEASE_BUILD} EasySaveLibrary.csproj

# Command Line Interface

cli: ${DEBUG_CLI_PATH}

cli-release: ${CLI_PATH}

${DEBUG_CLI_PATH}:
	@-cd cli/ && ${DEBUG_BUILD} EasySave.CLI.csproj
		
${CLI_PATH}:
	@-cd cli/ && ${RELEASE_BUILD} EasySave.CLI.csproj

run-cli: ${CLI_PATH}
	@-${CLI_PATH}

# Graphic User Interface

gui: ${DEBUG_GUI_PATH}

gui-release: ${GUI_PATH}

${DEBUG_GUI_PATH}:
	@-cd gui/ && ${DEBUG_BUILD} EasySave.GUI.csproj
		
${GUI_PATH}:
	@-cd gui/ && ${RELEASE_BUILD} EasySave.GUI.csproj

run-gui: ${GUI_PATH}
	@-${GUI_PATH}

# Server

server: ${DEBUG_SERVER_PATH}

server-release: ${SERVER_PATH}

${DEBUG_SERVER_PATH}:
	@-cd server/ && ${DEBUG_BUILD} EasySave.Server.csproj
		
${SERVER_PATH}:
	@-cd server/ && ${RELEASE_BUILD} EasySave.Server.csproj

run-server: ${SERVER_PATH}
	@-${SERVER_PATH}

# Remote

remote: ${DEBUG_REMOTE_PATH}

remote-release: ${REMOTE_PATH}

${DEBUG_REMOTE_PATH}:
	@-cd remote/ && ${DEBUG_BUILD} EasySave.Remote.csproj
		
${REMOTE_PATH}:
	@-cd remote/ && ${RELEASE_BUILD} EasySave.Remote.csproj

run-remote: ${REMOTE_PATH}
	@-${REMOTE_PATH}

# !! PHONIES !!

.PHONY: clean clean-bin clean-test test test-logger test-lib test-cli test-gui test-server test-remote

# Clean

clean: clean-bin clean-test clean-cli

clean-bin:
	@-rm -rf logger/bin lib/bin cli/bin gui/bin server/bin remote/bin

clean-test:
	@-rm -rf backups/*
	@-rm -rf cli/tests/bin

clean-cli:
	@rm -f state.json save.log

# Tests

test: clean test-logger test-lib test-cli test-gui test-server test-remote

test-logger: logger

test-lib: lib

test-cli: cli
	@-cd cli/tests/ && ${TEST}
	@-cli/test.sh

test-gui: gui

test-server: server

test-remote: remote

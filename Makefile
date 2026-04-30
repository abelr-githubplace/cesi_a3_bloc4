BUILD=dotnet build -verbosity q
TEST=dotnet test -verbosity q

DEBUG= --configuration Debug
RELEASE= --configuration Release
WIN_OS= --os win

DEBUG_BUILD=${BUILD} ${DEBUG} ${WIN_OS}
RELEASE_BUILD=${BUILD} ${RELEASE} ${WIN_OS}

BUILD=dotnet build ${TARGET_OS} --verbosity q
TEST=dotnet test ${TARGET_OS} --verbosity q --logger "console;verbosity=detailed"
CLEAN=dotnet clean --verbosity q

DEBUG_LOGGER_PATH=logger/bin/Debug/net10.0/win-x64/EasyLog.dll
DEBUG_LIBRARY_PATH=lib/bin/Debug/net10.0/win-x64/EasySaveLibrary.dll
DEBUG_CLI_PATH=cli/bin/Debug/net10.0/win-x64/EasySave.CLI.exe
DEBUG_GUI_PATH=gui/bin/Debug/net10.0/win-x64/EasySave.GUI.exe
DEBUG_SERVER_PATH=server/bin/Debug/net10.0/win-x64/EasySave.Server.exe
DEBUG_REMOTE_PATH=remote/bin/Debug/net10.0/win-x64/EasySave.Remote.exe

LOGGER_PATH=logger/bin/Release/net10.0/win-x64/EasyLog.dll
LIBRARY_PATH=lib/bin/Release/net10.0/win-x64/EasySaveLibrary.dll
CLI_PATH=cli/bin/Release/net10.0/win-x64/EasySave.CLI.exe
GUI_PATH=gui/bin/Release/net10.0/win-x64/EasySave.GUI.exe
SERVER_PATH=server/bin/Release/net10.0/win-x64/EasySave.Server.exe
REMOTE_PATH=remote/bin/Release/net10.0/win-x64/EasySave.Remote.exe

all: ${DEBUG_CLI_PATH} ${DEBUG_GUI_PATH} ${DEBUG_SERVER_PATH} ${DEBUG_REMOTE_PATH}

all-release: ${CLI_PATH} ${GUI_PATH} ${SERVER_PATH} ${REMOTE_PATH}

# Logger

logger: ${DEBUG_LOGGER_PATH}

logger-release: ${LOGGER_PATH}

${DEBUG_LOGGER_PATH}:
	@-${BUILD} ${DEBUG} logger/EasyLog.csproj

${LOGGER_PATH}:
	@-${BUILD} ${RELEASE} logger/EasyLog.csproj

# Library

lib: ${DEBUG_LIBRARY_PATH}

lib-release: ${LIBRARY_PATH}

${DEBUG_LIBRARY_PATH}:
	@-${BUILD} ${DEBUG} lib/EasySaveLibrary.csproj
		
${LIBRARY_PATH}:
	@-${BUILD} ${RELEASE} lib/EasySaveLibrary.csproj

# Command Line Interface

cli: ${DEBUG_CLI_PATH}

cli-release: ${CLI_PATH}

${DEBUG_CLI_PATH}:
	@-${BUILD} ${DEBUG} cli/EasySave.CLI.csproj
		
${CLI_PATH}:
	@-${BUILD} ${RELEASE} cli/EasySave.CLI.csproj

run-cli: ${CLI_PATH}
	@-${CLI_PATH}

# Graphic User Interface

gui: ${DEBUG_GUI_PATH}

gui-release: ${GUI_PATH}

${DEBUG_GUI_PATH}:
	@-${BUILD} ${DEBUG} gui/EasySave.GUI.csproj
		
${GUI_PATH}:
	@-${BUILD} ${RELEASE} gui/EasySave.GUI.csproj

run-gui: ${GUI_PATH}
	@-${GUI_PATH}

# Server

server: ${DEBUG_SERVER_PATH}

server-release: ${SERVER_PATH}

${DEBUG_SERVER_PATH}:
	@-${BUILD} ${DEBUG} server/EasySave.Server.csproj
		
${SERVER_PATH}:
	@-${BUILD} ${RELEASE} server/EasySave.Server.csproj

run-server: ${SERVER_PATH}
	@-${SERVER_PATH}

# Remote

remote: ${DEBUG_REMOTE_PATH}

remote-release: ${REMOTE_PATH}

${DEBUG_REMOTE_PATH}:
	@-${BUILD} ${DEBUG} remote/EasySave.Remote.csproj

${REMOTE_PATH}:
	@-${BUILD} ${RELEASE} remote/EasySave.Remote.csproj

run-remote: ${REMOTE_PATH}
	@-${REMOTE_PATH}

# !! PHONIES !!

.PHONY: clean clean-logger clean-lib clean-cli clean-gui clean-server clean-remote clean-test test test-logger test-lib test-cli test-gui test-server test-remote

# Clean

clean: clean-bin clean-test clean-cli

clean-logger:
	@-${CLEAN} logger/EasyLog.csproj
	@-${RM} -r logger/bin logger/tests/bin

clean-lib:
	@-${CLEAN} lib/EasySaveLibrary.csproj
	@-${RM} -r lib/bin lib/tests/bin

clean-cli:
	@-${CLEAN} cli/EasySave.CLI.csproj
	@-${RM} -r cli/bin cli/tests/bin
	@-${RM} state.json save.log

clean-gui:
	@-${CLEAN} cli/EasySave.GUI.csproj
	@-${RM} -r gui/bin gui/tests/bin
	@-${RM} state.json save.log

clean-server:
	@-${CLEAN} server/EasySave.Server.csproj
	@-${RM} -r server/bin server/tests/bin

clean-remote:
	@-${CLEAN} remote/EasySave.Remote.csproj
	@-${RM} -r remote/bin remote/tests/bin

clean-test:
	@-${RM} -r backups/* tests/*

# Tests

test: clean test-logger test-lib test-cli test-gui test-server test-remote

test-logger: clean-logger ${DEBUG_LOGGER_PATH}
	@-${TEST} logger/tests/

test-lib: clean-lib ${DEBUG_LIBRARY_PATH}
	@-${TEST} lib/tests/

test-cli: clean-cli ${DEBUG_CLI_PATH}
	@-${TEST} cli/tests/
	@-bash cli/test.sh

test-gui: clean-gui ${DEBUG_GUI_PATH}
	@-${TEST} gui/tests/

test-server: clean-server ${DEBUG_SERVER_PATH}
	@-${TEST} server/tests/

test-remote: clean-remote ${DEBUG_REMOTE_PATH}
	@-${TEST} remote/tests/

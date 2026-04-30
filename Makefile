DEBUG= --configuration Debug
RELEASE= --configuration Release
TARGET_OS= --os win

BUILD=dotnet build ${TARGET_OS} --verbosity q
TEST=dotnet test ${TARGET_OS} --logger "console;verbosity=detailed"
CLEAN=dotnet clean --verbosity q

LOGGER_PROJ=logger/EasyLog.csproj
LIBRARY_PROJ=lib/EasySaveLibrary.csproj
CLI_PROJ=cli/EasySave.CLI.csproj
GUI_PROJ=gui/EasySave.GUI.csproj
SERVER_PROJ=server/EasySave.Server.csproj
REMOTE_PROJ=remote/EasySave.Remote.csproj

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
	@-${BUILD} ${DEBUG} ${LOGGER_PROJ}

${LOGGER_PATH}:
	@-${BUILD} ${RELEASE} ${LOGGER_PROJ}

# Library

lib: ${DEBUG_LIBRARY_PATH}

lib-release: ${LIBRARY_PATH}

${DEBUG_LIBRARY_PATH}:
	@-${BUILD} ${DEBUG} ${LIBRARY_PROJ}
		
${LIBRARY_PATH}:
	@-${BUILD} ${RELEASE} ${LIBRARY_PROJ}

# Command Line Interface

cli: ${DEBUG_CLI_PATH}

cli-release: ${CLI_PATH}

${DEBUG_CLI_PATH}:
	@-${BUILD} ${DEBUG} ${CLI_PROJ}
		
${CLI_PATH}:
	@-${BUILD} ${RELEASE} ${CLI_PROJ}

run-cli: ${CLI_PATH}
	@-${CLI_PATH}

# Graphic User Interface

gui: ${DEBUG_GUI_PATH}

gui-release: ${GUI_PATH}

${DEBUG_GUI_PATH}:
	@-${BUILD} ${DEBUG} ${GUI_PROJ}
		
${GUI_PATH}:
	@-${BUILD} ${RELEASE} ${GUI_PROJ}

run-gui: ${GUI_PATH}
	@-${GUI_PATH}

# Server

server: ${DEBUG_SERVER_PATH}

server-release: ${SERVER_PATH}

${DEBUG_SERVER_PATH}:
	@-${BUILD} ${DEBUG} ${SERVER_PROJ}
		
${SERVER_PATH}:
	@-${BUILD} ${RELEASE} ${SERVER_PROJ}

run-server: ${SERVER_PATH}
	@-${SERVER_PATH}

# Remote

remote: ${DEBUG_REMOTE_PATH}

remote-release: ${REMOTE_PATH}

${DEBUG_REMOTE_PATH}:
	@-${BUILD} ${DEBUG} ${REMOTE_PROJ}

${REMOTE_PATH}:
	@-${BUILD} ${RELEASE} ${REMOTE_PROJ}

run-remote: ${REMOTE_PATH}
	@-${REMOTE_PATH}

# !! PHONIES !!

.PHONY:

# Clean

clean: clean-logger clean-lib clean-cli clean-gui clean-server clean-remote clean-test

clean-logger:
	@-${CLEAN} ${LOGGER_PROJ}
	@-${RM} -r logger/bin logger/tests/bin

clean-lib:
	@-${CLEAN} ${LIBRARY_PROJ}
	@-${RM} -r lib/bin lib/tests/bin

clean-cli:
	@-${CLEAN} ${CLI_PROJ}
	@-${RM} -r cli/bin cli/tests/bin
	@-${RM} state.json save.log config.json

clean-gui:
	@-${CLEAN} ${GUI_PROJ}
	@-${RM} -r gui/bin gui/tests/bin
	@-${RM} state.json save.log config.json

clean-server:
	@-${CLEAN} ${SERVER_PROJ}
	@-${RM} -r server/bin server/tests/bin
	@-${RM} state.json save.log config.json

clean-remote:
	@-${CLEAN} ${REMOTE_PROJ}
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

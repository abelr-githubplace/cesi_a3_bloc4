MAKE=make --quiet

DEBUG= --configuration Debug
RELEASE= --configuration Release
TARGET_OS= --os win

BUILD=dotnet build ${TARGET_OS} --verbosity q
TEST=dotnet test ${TARGET_OS} --logger "console;verbosity=detailed"
CLEAN=dotnet clean --verbosity q

LOGGER_PROJ=logger/EasyLog.csproj
CRYPTO_PROJ=crypto/EasyCrypt.csproj
LIBRARY_PROJ=lib/EasySaveLibrary.csproj
CLI_PROJ=cli/EasySave.CLI.csproj
GUI_PROJ=gui/EasySave.GUI.csproj
SERVER_PROJ=server/EasySave.Server.csproj
REMOTE_PROJ=remote/EasySave.Remote.csproj
CONSOLE_PROJ=console/EasySave.Console.csproj

DEBUG_LOGGER_PATH=logger/bin/Debug/net8.0/win-x64/EasyLog.dll
DEBUG_CRYPTO_PATH=crypto/bin/Debug/net8.0/win-x64/EasyCrypt.dll
DEBUG_LIBRARY_PATH=lib/bin/Debug/net8.0/win-x64/EasySaveLibrary.dll
DEBUG_CLI_PATH=cli/bin/Debug/net8.0/win-x64/EasySave.CLI.exe
DEBUG_GUI_PATH=gui/bin/Debug/net8.0-windows/win-x64/EasySave.GUI.exe
DEBUG_SERVER_PATH=server/bin/Debug/net8.0/win-x64/EasySave.Server.exe
DEBUG_REMOTE_PATH=remote/bin/Debug/net8.0/win-x64/EasySave.Remote.exe
DEBUG_CONSOLE_PATH=console/bin/Debug/net8.0-windows/win-x64/EasySave.Console.exe

LOGGER_PATH=logger/bin/Release/net8.0/win-x64/EasyLog.dll
CRYPTO_PATH=crypto/bin/Release/net8.0/win-x64/EasyCrypt.dll
LIBRARY_PATH=lib/bin/Release/net8.0/win-x64/EasySaveLibrary.dll
CLI_PATH=cli/bin/Release/net8.0/win-x64/EasySave.CLI.exe
GUI_PATH=gui/bin/Release/net8.0-windows/win-x64/EasySave.GUI.exe
SERVER_PATH=server/bin/Release/net8.0/win-x64/EasySave.Server.exe
REMOTE_PATH=remote/bin/Release/net8.0/win-x64/EasySave.Remote.exe
CONSOLE_PATH=console/bin/Release/net8.0-windows/win-x64/EasySave.Console.exe

all: ${DEBUG_CLI_PATH} ${DEBUG_GUI_PATH} ${DEBUG_SERVER_PATH} ${DEBUG_REMOTE_PATH} ${DEBUG_CONSOLE_PATH}

all-release: ${CLI_PATH} ${GUI_PATH} ${SERVER_PATH} ${REMOTE_PATH} ${CONSOLE_PATH}

# Logger

logger: ${DEBUG_LOGGER_PATH}

logger-release: ${LOGGER_PATH}

${DEBUG_LOGGER_PATH}:
	@-${BUILD} ${DEBUG} ${LOGGER_PROJ}

${LOGGER_PATH}:
	@-${BUILD} ${RELEASE} ${LOGGER_PROJ}

# Crypto

crypto: ${DEBUG_CRYPTO_PATH}

crypto-release: ${CRYPTO_PATH}

${DEBUG_CRYPTO_PATH}:
	@-${BUILD} ${DEBUG} ${CRYPTO_PROJ}

${CRYPTO_PATH}:
	@-${BUILD} ${RELEASE} ${CRYPTO_PROJ}

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

# Console (WPF client déporté)

console: ${DEBUG_CONSOLE_PATH}

console-release: ${CONSOLE_PATH}

${DEBUG_CONSOLE_PATH}:
	@-${BUILD} ${DEBUG} ${CONSOLE_PROJ}

${CONSOLE_PATH}:
	@-${BUILD} ${RELEASE} ${CONSOLE_PROJ}

run-console: ${CONSOLE_PATH}
	@-${CONSOLE_PATH}

# !! PHONIES !!

.PHONY:

# Clean

clean: clean-logger clean-crypto clean-lib clean-cli clean-gui clean-server clean-remote clean-console clean-test

clean-logger:
	@-${CLEAN} ${LOGGER_PROJ}
	@-${RM} -r logger/bin logger/tests/bin

clean-crypto:
	@-${CLEAN} ${CRYPTO_PROJ}
	@-${RM} -r crypto/bin crypto/tests/bin

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

clean-console:
	@-${CLEAN} ${CONSOLE_PROJ}
	@-${RM} -r console/bin

clean-test:
	@-${RM} -r backups/* tests/*

# Tests

test: clean test-logger test-crypto test-lib test-cli test-gui test-server test-remote

test-logger: clean-logger ${DEBUG_LOGGER_PATH}
	@-${TEST} logger/tests/
	
test-crypto: clean-crypto ${DEBUG_CRYPTO_PATH}
	@-${TEST} crypto/tests/

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

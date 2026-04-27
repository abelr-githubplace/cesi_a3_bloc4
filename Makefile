DEBUG= --configuration Debug
RELEASE= --configuration Release

all: cli gui server remote

all-release: cli-release gui-release server-release remote-release

logger:
	@cd logger/
	@dotnet build ${DEBUG} EasyLog.csproj

logger-release:
	@cd logger/
	@dotnet build ${RELEASE} EasyLog.csproj
	
lib: logger
	@cd lib/
	@dotnet build ${DEBUG} EasySaveLibrary.csproj
		
lib-release: logger-release
	@cd lib/
	@dotnet build ${RELEASE} EasySaveLibrary.csproj

cli: logger lib
	@cd cli/
	@dotnet build ${DEBUG} EasySave.CLI.csproj
		
cli-release: logger-release lib-release
	@cd cli/
	@dotnet build ${RELEASE} EasySave.CLI.csproj

gui: logger lib
	@cd gui/
	@dotnet build ${DEBUG} EasySave.GUI.csproj
		
gui-release: logger-release lib-release
	@cd gui/
	@dotnet build ${RELEASE} EasySave.GUI.csproj

server: logger lib
	@cd server/
	@dotnet build ${DEBUG} EasySave.Server.csproj
		
server-release: logger-release lib-release
	@cd server/
	@dotnet build ${RELEASE} EasySave.Server.csproj

remote: logger lib
	@cd remote/
	@dotnet build ${DEBUG} EasySave.Remote.csproj
		
remote-release: logger-release lib-release
	@cd remote/
	@dotnet build ${RELEASE} EasySave.Remote.csproj

.PHONY: clean

clean:
	@rm -rf logger/bin lib/bin cli/bin gui/bin server/bin remote/bin
	@rm -rf backups/*

test: clean all test-logger test-lib test-cli test-gui test-server test-remote

test-logger:
test-lib:
test-cli:
	@cd cli/
	test.sh
test-gui:
test-server:
test-remote:
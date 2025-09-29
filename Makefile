.PHONY: runtime build test

runtime:
	make -C regex-runtime
	sudo make -C regex-runtime install

build: runtime
	dotnet publish -c release

run: runtime
	dotnet run --project regex

debug: build
	gdb regex/bin/Release/net9.0/regex

run:
	dotnet run --project regex

test:
	dotnet test --collect:"XPlat Code Coverage"
	pycobertura show \
		 regex.Test/TestResults/$(shell ls -t regex.Test/TestResults | head -n1)/coverage.cobertura.xml \
		-f html -o regex.Test/coverage.html -p ${HOME}/proj/regex-cs/regex/
	

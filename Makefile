.PHONY: build test

build:
	dotnet build

test:
	dotnet test --collect:"XPlat Code Coverage"
	pycobertura show \
		 regex.Test/TestResults/$(shell ls -t regex.Test/TestResults | head -n1)/coverage.cobertura.xml \
		-f html -o regex.Test/coverage.html -p ${HOME}/proj/regex-cs/regex/
	

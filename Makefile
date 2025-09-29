.PHONY: runtime build test

runtime:
	make -C regex-runtime
	sudo make -C regex-runtime install

build: runtime
	dotnet publish -c release

debug: build
	gdb regex/bin/Release/net9.0/regex

run: runtime
	dotnet run --project regex
	mkdir -p vis
	dot /tmp/regex-cs-nfa.dot -Tsvg -o vis/nfa.svg

test: runtime
	dotnet test -l 'console;verbosity=detailed'
	

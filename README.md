# Non-backtracking NFA regex engine

Implementation of a regex engine with stable running time and memory consumption.
It runs in `O(m*n)` time with `O(m)` memory, where `m` is the size of the regex, and `n` is the size of the input.

The project consists of two parts: frontend written in C# and C backend.
IT also includes x86-64 JIT compiler that makes matching ~5x faster compared to the standard implementation.

I haven't systematically collected and published benchmarks, but here's what I've found:

* JIT produces code ~5x faster than the standard C implementation.
* My engine (JIT) matches as fast as .NET and Python on small regexes,
  but slower than Perl and DFA engines.
* Performance degrades linearly with the growth of the regex.
* It can handle cases like `(a*)*bz` with the string `aaa...az` in linear time,
  while implementations mentioned above suffer from catastrophic backtracking.
* It compiles and matches expressions like `(a|b)*a(a|b){n}` in linear time,
  while DFA implementations require exponential run time and memory,
  since the minimal DFA for that expression has `2^n` nodes.

Compared to DFA-based approaches,
it is relatively easy to implement extensions for non-backtracking NFAs, such as submatches and assertions.
However, backreferences require backtracking.

Basic optimizations in the JIT code generator may significantly increase performance.
The current implementation is far from being effective - it creates a number of unnecessary branches,
suffers from branch misprediction and CPU stalls.

The reason I separated this project into C# and C parts is
that I was doing it for my university course,
and the requirement was to make interop between C# program and C library.

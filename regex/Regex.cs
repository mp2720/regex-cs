using Regex.Parser;

namespace Regex
{

    public class RegexToNFA
    {
        /// <summary>
        /// Table of 256 classes, i-th represents encoded by \i and could be null.
        /// </summary>
        private NFA.CharClass[] BuiltinClassesTable { get; init; }

        /// <summary>
        /// Construct a new regex to NFA converter. Only ASCII characters are allowed.
        /// </summary>
        /// <param name="builtinClasses">
        /// Builtin classes should not use names (,),[,],+,*,?,.,&#92;,x,X,^,|,-,n,0,r,t,a,b,v,
        /// since those are reserved for escape sequences.
        /// </param>
        public RegexToNFA(List<(char, NFA.CharClass)> builtinClasses)
        {
            BuiltinClassesTable = new NFA.CharClass[256];
            foreach (var cl in builtinClasses)
                BuiltinClassesTable[cl.Item1] = cl.Item2;
        }

        public static RegexToNFA WithDefaultBuiltinClasses()
        {
            NFA.CharClass
                digits = new([new('0', '9')]),
                wordChar = new([new('A', 'Z'), new('a', 'z'), new('0', '9'), new('_', '_')]),
                space = NFA.CharClass.List(' ', '\n', '\r', '\t', '\v', '\f');
            var classes = new List<(char, NFA.CharClass)>() {
                ('d', digits),
                ('D', digits.Invert()),
                ('w', wordChar),
                ('W', wordChar.Invert()),
                ('s', space),
                ('S', space.Invert())
            };
            return new(classes);
        }

        private char HexDigit(StringParser p) =>
            (char)p.Or(
                p => p.Char(c => '0' <= c && c <= '9') - '0',
                p => p.Char(c => 'a' <= c && c <= 'f') - 'a' + 10,
                p => p.Char(c => 'A' <= c && c <= 'F') - 'A' + 10
            );

        private char HexByte(StringParser p)
        {
            return (char)(HexDigit(p) * 16 + HexDigit(p));
        }

        // without \
        private char Escaped(StringParser p)
        {
            char c = p.Char();
            return c switch
            {
                'x' or 'X' => HexByte(p),
                ']' or '[' or '\\' or '(' or ')' or '^' or '.' or '?' or '+' or '*' or '|' or '-' => c,
                'n' => '\n',
                '0' => '\0',
                'r' => '\r',
                't' => '\t',
                'a' => '\a',
                'v' => '\v',
                _ => throw new InvalidSyntaxException(p),
            };
        }

        private char CharRangeBoundary(StringParser p)
        {
            return p.Or(
                p =>
                {
                    p.Char('\\');
                    return Escaped(p);
                },
                p => p.Char(c => 0x20 <= c && c <= 0x7e && c != ']' && c != '\\' && c != '-')
            );
        }

        private NFA.CharRange CharRange(StringParser p)
        {
            char start = CharRangeBoundary(p);
            var end = p.Optional(p =>
            {
                p.Char('-');
                return CharRangeBoundary(p);
            });
            if (end.Set)
            {
                if (start > end.Value)
                    throw new InvalidSyntaxException(p, "invalid range boundaries");
                return new NFA.CharRange(start, end.Value);
            }
            else
            {
                return new NFA.CharRange(start, start);
            }
        }

        private NFA.CharClass CharClass(StringParser p)
        {
            p.Char('[');
            var exceptFlag = p.Optional(p => p.Char('^'));

            var ranges = new List<NFA.CharRange>() { CharRange(p) }; // at least one is expected
            while (true)
            {
                var range = p.Optional(CharRange);
                if (!range.Set)
                    break;
                ranges.Add(range.Value);
            }

            p.Char(']');

            return new NFA.CharClass(ranges, exceptFlag.Set);
        }

        private NFA.CharClass CharMatch(StringParser p)
        {
            return p.Or(
                CharClass, // Ex.: [a-bZ]
                p => // Dot
                {
                    p.Char('.');
                    return NFA.CharClass.All();
                },
                p => // builtin class
                {
                    p.Char('\\');
                    return p.Or(
                        p =>
                        {
                            char c = p.Char();
                            return BuiltinClassesTable[c] ?? throw new InvalidSyntaxException(p);
                        },
                        p =>
                        {
                            char c = Escaped(p);
                            return NFA.CharClass.SingleChar(c);
                        }
                    );
                },
                p => // other
                {
                    char c = p.Char(c => 0x20 <= c && c <= 0x7e
                         && c != '\\' && c != '*' && c != '?' && c != ')' && c != '|'
                         && c != '+' && c != '[' && c != ']' && c != '(' && c != '.');
                    return NFA.CharClass.SingleChar(c);
                }
            );
        }

        private (NFA.State s, NFA.State e) Group(StringParser p)
        {
            p.Char('(');
            var ret = Alternative(p);
            p.Char(')');
            return ret;
        }

        // Following functions try to match regex language rules,
        // and on success generate a part of NFA.
        // They return start and end state of the sub-NFA following those rules:
        //  1. End state of the sub-NFA never has an outgoing Next1 arrow. 
        //  2. Start and end states may coincide.
        //  3. It is always better for clarity to generate redundant states, since those can be easily
        //     optimized out later.
        //  4. ε-only loops are allowed.
        //  5. End may be an ε-state with Next2 arrow.
        //
        // This approach (as well as many other parts of this project) is insipred
        // by Russ Cox's article: https://swtch.com/~rsc/regexp/regexp1.html

        private (NFA.State s, NFA.State e) Atom(StringParser p)
        {
            return p.Or(
                Group,
                p =>
                {
                    // -> s ->
                    var c = CharMatch(p);
                    var s = NFA.State.MakeConsuming(c);
                    return (s, s);
                }
            );
        }

        private (NFA.State s, NFA.State e) AtomQuantified(StringParser p)
        {
            var (s, e) = Atom(p);
            return p.Or(
                p =>
                {
                    // -> s1 -> s -> ... -> e -> e1 ->
                    //     \                     ^
                    //      \                    |
                    //       *-------------------*
                    p.Char('?');
                    var e1 = NFA.State.MakeEpsilon();
                    e.Next1 = e1;
                    var s1 = NFA.State.MakeEpsilon(next1: s, next2: e1);
                    return (s1, e1);
                },
                p =>
                {
                    // -> s -> ... -> e -> e1 ->
                    //    ^                /
                    //     \              /
                    //      *---- b <----*
                    p.Char('+');
                    var b = NFA.State.MakeEpsilon(next1: s, back: true);
                    var e1 = NFA.State.MakeEpsilon(next2: b);
                    e.Next1 = e1;
                    return (s, e1);
                },
                p =>
                {
                    //      +-> s -> ... -> e
                    //     /                |
                    // -> s1 <----- b <-----+
                    //     \
                    //      *-----> e1 ----->
                    p.Char('*');

                    var e1 = NFA.State.MakeEpsilon();
                    var s1 = NFA.State.MakeEpsilon(next1: s, next2: e1);
                    var b = NFA.State.MakeEpsilon(next1: s1, back: true);
                    e.Next1 = b;
                    return (s1, e1);
                },
                p => (s, e) // no quantifier
            );
        }

        private (NFA.State s, NFA.State e) Concat(StringParser p)
        {
            var (s, e) = AtomQuantified(p);
            while (true)
            {
                var optional = p.Optional(AtomQuantified);
                if (!optional.Set)
                    break;

                // -> s -> ... -> e -> s1 -> ... -> e1 ->

                var (s1, e1) = optional.Value;
                e.Next1 = s1;
                e = e1;
            }
            return (s, e);
        }

        private (NFA.State, NFA.State) Alternative(StringParser p)
        {
            var (s1, e1) = Concat(p);
            var opt = p.Optional(p =>
            {
                p.Char('|');
                return Alternative(p);
            });
            if (!opt.Set)
                return (s1, e1);

            //      +-> s1 -> ... -> e1 --> e ->
            //     /                        ^
            //    /                        /
            // -> s --> s2 -> ... -> e2 --*

            var (s2, e2) = opt.Value;
            var s = NFA.State.MakeEpsilon(next1: s1, next2: s2);
            var e = NFA.State.MakeEpsilon();
            e1.Next1 = e;
            e2.Next1 = e;
            return (s, e);
        }

        private void AssignIndexDFS(List<NFA.State> reachableStates, NFA.State? state, ref int index)
        {
            if (state == null || state.Index >= 0)
                return;

            state.Index = index++;
            reachableStates.Add(state);
            AssignIndexDFS(reachableStates, state.Next1, ref index);
            AssignIndexDFS(reachableStates, state.Next2, ref index);
        }

        public NFA.Automaton Convert(string expr)
        {
            var p = new StringParser(expr, 0);

            var (start, end) = Alternative(p);
            p.EOF();

            var source = NFA.State.MakeEpsilon(next1: start);
            var sink = NFA.State.MakeEpsilon();
            end.Next1 = sink;

            List<NFA.State> states = [];
            int index = 0;
            AssignIndexDFS(states, source, ref index);

            return new NFA.Automaton(source, states);
        }
    }
}

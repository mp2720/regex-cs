namespace Regex.Parser
{
    public class RegexParser
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
        public RegexParser(List<(char, NFA.CharClass)> builtinClasses)
        {
            BuiltinClassesTable = new NFA.CharClass[256];
            foreach (var cl in builtinClasses)
                BuiltinClassesTable[cl.Item1] = cl.Item2;
        }

        /// <summary>
        /// Create new NFA builder with classes for digits, word chars, spaces, and their negations.
        /// </summary>
        public static RegexParser WithDefaultBuiltinClasses()
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

        private char HexDigit(Parser p) =>
            (char)p.Or(
                p => p.Char(c => '0' <= c && c <= '9') - '0',
                p => p.Char(c => 'a' <= c && c <= 'f') - 'a' + 10,
                p => p.Char(c => 'A' <= c && c <= 'F') - 'A' + 10
            );

        private char HexByte(Parser p)
        {
            return (char)(HexDigit(p) * 16 + HexDigit(p));
        }

        // without \
        private char Escaped(Parser p)
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
                _ => throw new ParsingException(p),
            };
        }

        private char CharRangeBoundary(Parser p)
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

        private NFA.CharRange CharRange(Parser p)
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
                    throw new ParsingException(p, "invalid range boundaries");
                return new NFA.CharRange(start, end.Value);
            }
            else
            {
                return new NFA.CharRange(start, start);
            }
        }

        private NFA.CharClass CharClass(Parser p)
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

        private NFA.CharClass CharMatch(Parser p)
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
                            return BuiltinClassesTable[c] ?? throw new ParsingException(p);
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

        private (NFA.State s, NFA.State e) Group(Parser p)
        {
            p.Char('(');
            var ret = Alternative(p);
            p.Char(')');
            return ret;
        }

        // Following functions will try to match regex language rules,
        // and on success generate a part of NFA.
        // They return start and end state of the sub-NFA following these rules:
        //  1. End state of the sub-NFA never has an outgoing Next1 arrow. 
        //  2. Start and end states may coincide.
        //  3. It is always better for clarity to generate redundant states, since those can be easily
        //     optimized out later.
        //  4. ε-only loops are allowed.
        //  5. End may be an ε-state with Next2 arrow.
        //  6. Back arrows in loops are marked.
        //
        // This approach (as well as many other parts of this project) is insipred
        // by Russ Cox's article: https://swtch.com/~rsc/regexp/regexp1.html

        private (NFA.State s, NFA.State e) Atom(Parser p)
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

        private (NFA.State s, NFA.State e) AtomQuantified(Parser p)
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
                    var s1 = NFA.State.MakeEpsilon();
                    e.AddNext(e1);
                    s1.AddNext(s, e1);
                    return (s1, e1);
                },
                p =>
                {
                    // -> s -> ... -> e -> e1 ->
                    //    ^                /
                    //     \              /
                    //      *---- b <----*
                    p.Char('+');
                    var b = NFA.State.MakeBack();
                    var e1 = NFA.State.MakeEpsilon();
                    e.AddNext(e1);
                    e1.AddNext(b);
                    b.AddNext(s);
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

                    var s1 = NFA.State.MakeEpsilon();
                    var b = NFA.State.MakeBack();
                    var e1 = NFA.State.MakeEpsilon();
                    s1.AddNext(s, e1);
                    e.AddNext(b);
                    b.AddNext(s1);
                    return (s1, e1);
                },
                p => (s, e) // no quantifier
            );
        }

        private (NFA.State s, NFA.State e) Concat(Parser p)
        {
            var (s, e) = AtomQuantified(p);
            while (true)
            {
                var optional = p.Optional(AtomQuantified);
                if (!optional.Set)
                    break;

                // -> s -> ... -> e -> s1 -> ... -> e1 ->

                var (s1, e1) = optional.Value;
                e.AddNext(s1);
                e = e1;
            }
            return (s, e);
        }

        private (NFA.State s, NFA.State e) Alternative(Parser p)
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
            var s = NFA.State.MakeEpsilon();
            var e = NFA.State.MakeEpsilon();
            s.AddNext(s1, s2);
            e1.AddNext(e);
            e2.AddNext(e);
            return (s, e);
        }

        private void AssignIndexDFS(List<NFA.State> reachableStates, NFA.State? state, ref int index)
        {
            if (state == null || state.Index >= 0)
                return;

            state.Index = index++;
            reachableStates.Add(state);
            foreach (var nextState in state.Next)
                AssignIndexDFS(reachableStates, nextState, ref index);
        }

        public NFA.Automaton Convert(string expr)
        {
            var p = new Parser(expr, 0);

            var (s, e) = Alternative(p);
            p.EOF();

            var source = NFA.State.MakeEpsilon();
            var sink = NFA.State.MakeEpsilon();
            source.AddNext(s);
            e.AddNext(sink);

            List<NFA.State> states = [];
            int index = 0;
            AssignIndexDFS(states, source, ref index);

            return new NFA.Automaton([source], sink, states);
        }
    }
}

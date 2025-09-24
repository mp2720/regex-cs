using Regex.Parser;

namespace Regex
{

    public class RegexToNFA
    {
        private int stateIndex = 0;

        /// <summary>
        /// Table of 256 classes, i-th represents encoded by \i and could be null.
        /// </summary>
        private NFA.CharClass[] BuiltinClassesTable { get; init; }

        private readonly List<NFA.State> states = [];

        /// <summary>
        ///     Construct a new regex to NFA converter. Only ASCII characters are allowed.
        /// </summary>
        /// <param name="builtinClasses">
        ///     Builtin classes should not use names (,),[,],+,*,?,.,&#92;,x,X,^,|,-,n,0,r,t,a,b,v,
        ///     since those are reserved for escape sequences.
        /// </param>
        public RegexToNFA(List<(char, NFA.CharClass)> builtinClasses)
        {
            BuiltinClassesTable = new NFA.CharClass[256];
            foreach (var cl in builtinClasses)
                BuiltinClassesTable[cl.Item1] = cl.Item2;
        }

        // Call when sub-expression is determined since those states are collected to be returned
        // as a conversion result!
        private NFA.State NewState()
        {
            var state = new NFA.State(stateIndex++);
            states.Add(state);
            return state;
        }

        private static char HexDigit(StringParser p) =>
            (char)p.Or(
                p => p.Char(c => '0' <= c && c <= '9') - '0',
                p => p.Char(c => 'a' <= c && c <= 'f') - 'a' + 10,
                p => p.Char(c => 'A' <= c && c <= 'F') - 'A' + 10
            );

        private static char HexByte(StringParser p)
        {
            return (char)(HexDigit(p) * 16 + HexDigit(p));
        }

        // without \
        private static char Escaped(StringParser p)
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
                'b' => '\b',
                'v' => '\v',
                _ => throw new InvalidSyntaxException(p),
            };
        }

        private static char CharRangeBoundary(StringParser p)
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

        private (NFA.State s, NFA.State e) Atom(StringParser p)
        {
            return p.Or(
                p =>
                {
                    p.Char('(');
                    var ret = Alternative(p);
                    p.Char(')');
                    return ret;
                },
                p =>
                {
                    // s --------> e
                    var c = CharMatch(p);
                    var s = NewState();
                    var e = NewState();
                    s.Transition(c, e);
                    return (s, e);
                }
            );
        }

        private (NFA.State s, NFA.State e) AtomQuantified(StringParser p)
        {
            var (s, e) = Atom(p);
            return p.Or(
                p =>
                {
                    // s -> ... -> e -> e1
                    //  \               ^
                    //   \              |
                    //    *-------------*
                    p.Char('?');
                    var e1 = NewState();
                    e.Transition(e1);
                    s.Transition(e1);
                    return (s, e1);
                },
                p =>
                {
                    // s -> ... -> e -> e1
                    // ^                /
                    //  \              /
                    //   *------------*
                    p.Char('+');
                    var e1 = NewState();
                    e.Transition(e1);
                    e1.Transition(s);
                    return (s, e);
                },
                p =>
                {
                    // s1 -> s -> ... -> e -> e1
                    //  \^              /     ^
                    //   \\            /     / 
                    //    \*----------*     /  
                    //     \               /   
                    //      *-------------*
                    p.Char('*');
                    var s1 = NewState();
                    var e1 = NewState();
                    s1.Transition(s);
                    s1.Transition(e1);
                    e.Transition(e1);
                    e.Transition(s1);
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

                // s -> ... -> e -> s1 -> ... -> e1

                var (s1, e1) = optional.Value;
                e.Transition(s1);
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

            //    +-> s1 -> ... -> e1 --> e
            //   /                        ^
            //  /                        /
            // s --> s2 -> ... -> e2 ---*

            var (s2, e2) = opt.Value;
            var s = NewState();
            var e = NewState();
            s.Transition(s1);
            s.Transition(s2);
            e1.Transition(e);
            e2.Transition(e);
            return (s, e);
        }

        public NFA.Automaton Convert(string expr)
        {
            states.Clear();
            stateIndex = 0;
            var p = new StringParser(expr, 0);

            var (start, end) = Alternative(p);

            var sink = NewState();
            end.Transition(sink);
            return new NFA.Automaton(start, sink, states.AsReadOnly());
        }
    }
}

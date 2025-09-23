using Parse;

namespace Regex
{
    /// <summary>
    /// Range of 8-bit characters [From..To].
    /// </summary>
    public record CharRange(char From, char To) { }

    public record CharClass(List<CharRange> Ranges, bool Inverted = false)
    {
        public static CharClass All()
        {
            return new CharClass([], true);
        }

        public static CharClass SingleChar(char c)
        {
            return new CharClass([new(c, c)]);
        }

        public static CharClass List(params char[] chs)
        {
            var ranges = new List<CharRange>();
            foreach (char c in chs)
                ranges.Add(new CharRange(c, c));
            return new CharClass(ranges);
        }
    }

    /// <summary>
    /// Directed transition between two NFA states.
    /// </summary>
    /// <param name="Condition">
    /// Null if transition is unconditional
    /// </summary>
    public record NFATrans(CharClass? Condition, NFAState To) { }

    public class NFAState
    {
        public int Index { get; init; }
        public List<NFATrans> Transitions { get; } = [];

        public NFAState(int index)
        {
            Index = index;
        }

        public void Transition(CharClass condition, NFAState to)
        {
            Transitions.Add(new NFATrans(condition, to));
        }

        /// <summary>
        /// Make epsilon transition.
        /// </summary>
        public void Transition(NFAState to)
        {
            Transitions.Add(new NFATrans(null, to));
        }
    }

    public class RegexToNFA
    {
        private int stateIndex = 0;

        /// <summary>
        /// Table of 256 classes, i-th represents encoded by \i and could be null.
        /// </summary>
        private CharClass[] BuiltinClassesTable { get; init; }

        /// <summary>
        ///     Construct a new regex to NFA converter. Only ASCII characters are allowed.
        /// </summary>
        /// <param name="builtinClasses">
        ///     Builtin classes should not use names (,),[,],+,*,?,.,&#92;,x,X,^,|,-,n,0,r,t,a,b,v,
        ///     since those are reserved for escape sequences.
        /// </param>
        public RegexToNFA(List<(char, CharClass)> builtinClasses)
        {
            BuiltinClassesTable = new CharClass[256];
            foreach (var cl in builtinClasses)
                BuiltinClassesTable[(int)cl.Item1] = cl.Item2;
        }

        private NFAState newState()
        {
            return new NFAState(stateIndex++);
        }

        private char HexDigit(Parser p) =>
            (char)p.Or(
                p => p.Char(c => '0' <= c && c <= '9') - '0',
                p => p.Char(c => 'a' <= c && c <= 'f') - 'a',
                p => p.Char(c => 'A' <= c && c <= 'F') - 'A'
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
                'b' => '\b',
                'v' => '\v',
                _ => throw new InvalidSyntaxException(p),
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

        private CharRange CharRange(Parser p)
        {
            char start = CharRangeBoundary(p);
            var end = p.Optional(p =>
            {
                p.Char('-');
                return CharRangeBoundary(p);
            });
            if (end.Set)
                return new CharRange(start, end.Value);
            else
                return new CharRange(start, start);
        }

        private CharClass CharClass(Parser p)
        {
            p.Char('[');
            var exceptFlag = p.Optional(p => p.Char('^'));

            var ranges = new List<CharRange>() { CharRange(p) }; // at least one is expected
            while (true)
            {
                var range = p.Optional(CharRange);
                if (!range.Set)
                    break;
                ranges.Add(range.Value);
            }

            p.Char(']');

            return new CharClass(ranges, exceptFlag.Set);
        }

        private CharClass CharMatch(Parser p)
        {
            return p.Or(
                CharClass, // Ex.: [a-bZ]
                p => // Dot
                {
                    p.Char('.');
                    return Regex.CharClass.All();
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
                            return Regex.CharClass.SingleChar(c);
                        }
                    );
                },
                p => // other
                {
                    char c = p.Char(c => 0x20 <= c && c <= 0x7e
                         && c != '\\' && c != '*' && c != '?' && c != ')' && c != '|'
                         && c != '+' && c != '[' && c != ']' && c != '(' && c != '.');
                    return Regex.CharClass.SingleChar(c);
                }
            );
        }

        private (NFAState s, NFAState e) Atom(Parser p)
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
                    var s = newState();
                    var e = newState();
                    s.Transition(c, e);
                    return (s, e);
                }
            );
        }

        private (NFAState s, NFAState e) AtomQuantified(Parser p)
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
                    var e1 = newState();
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
                    var e1 = newState();
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
                    var s1 = newState();
                    var e1 = newState();
                    s1.Transition(s);
                    s1.Transition(e1);
                    e.Transition(e1);
                    e.Transition(s1);
                    return (s1, e1);
                },
                p => (s, e) // no quantifier
            );
        }

        private (NFAState s, NFAState e) Concat(Parser p)
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

        private (NFAState, NFAState) Alternative(Parser p)
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
            var s = newState();
            var e = newState();
            s.Transition(s1);
            s.Transition(s2);
            e1.Transition(e);
            e2.Transition(e);
            return (s, e);
        }

        public (NFAState start, NFAState end) Convert(string expr)
        {
            var p = new Parser(expr, 0);
            return Alternative(p);
        }
    }
}

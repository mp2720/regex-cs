using System.Diagnostics;

namespace Parse
{
    public struct Maybe<T>
    {
        private T _value;
        public T Value
        {
            get
            {
                if (!Set)
                    throw new InvalidOperationException("Optional value is empty");
                return _value;
            }
            private init { _value = value; }
        }
        public bool Set { get; private init; }

        public static Maybe<T> Some(T value)
        {
            return new Maybe<T> { Set = true, Value = value };
        }

        public static Maybe<T> Empty()
        {
            return new Maybe<T> { Set = false };
        }
    }

    public class InvalidSyntaxException : Exception
    {
        public InvalidSyntaxException(Parser p) : base($"Invalid syntax at character {p.Index}") { }
        public InvalidSyntaxException(string reason) : base($"Invalid syntax: {reason}") { }
    }

    public record Parser
    {
        public String Text { get; private set; }
        public int Index { get; private set; }

        public Parser(String text, int index)
        {
            this.Text = text;
            this.Index = index;
        }

        /// <summary>
        /// Try one of the options in order they were given.
        /// If one of the options matched, the result is immediatly returned.
        /// If one of the options consumed more than one character and failed, then <c>Or</c> fails
        /// (thus no devastating backtracking is possible).
        /// If none matched, Or fails.
        /// </summary>
        public T Or<T>(params Func<Parser, T>[] parseFuncs)
        {
            int savedIndex = Index;
            for (int i = 0; i < parseFuncs.Length; ++i)
            {
                try
                {
                    return parseFuncs[i](this);
                }
                catch (InvalidSyntaxException)
                {
                    if (Index > savedIndex + 1 || i == parseFuncs.Length - 1)
                        throw;
                    Index = savedIndex;
                    continue;
                }
            }
            throw new UnreachableException();
        }

        public char Char()
        {
            if (Index == Text.Length)
                throw new InvalidSyntaxException("unexpected EOF");
            return Text[Index++];
        }

        public char Char(Predicate<char> p)
        {
            char c = Char();
            if (p(c))
                return c;
            else
                throw new InvalidSyntaxException(this);
        }

        public char Char(char c)
        {
            return Char(c1 => c1 == c);
        }

        public string String(string s)
        {
            foreach (char c in s)
            {
                Char(c);
            }
            return s;
        }

        /// <summary>
        /// Adapter from <c>InvalidSyntaxException</c> to <c>Maybe</c>.
        /// Does not backtrack (except the cases when only one character was read).
        /// </summary>
        public Maybe<T> Optional<T>(Func<Parser, T> parseFunc)
        {
            int savedIndex = Index;
            try
            {
                return Maybe<T>.Some(parseFunc(this));
            }
            catch (InvalidSyntaxException)
            {
                Index = savedIndex;
                return Maybe<T>.Empty();
            }
        }
    }
}

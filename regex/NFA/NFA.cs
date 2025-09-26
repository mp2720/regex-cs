namespace Regex.NFA
{
    /// <summary>
    /// Range of 8-bit characters [start..end].
    /// </summary>
    public record CharRange(char start, char end) { }

    public record CharClass(IReadOnlyList<CharRange> ranges, bool inverted = false)
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

        /// <summary>
        /// Return an inverted copy.
        /// </summary>
        public CharClass Invert()
        {
            return new CharClass(ranges, !inverted);
        }
    }

    /// <summary>
    /// There are two different types of NFA states: ε and non-ε.
    /// ε-states do not consume/generate characters, and may have from 0 to 2 outgoing arrows.
    /// State of this type also may be a "back" state - intermediate state in a loop back path.
    /// Non-ε-states consume/generate one character at a time and have no more than 1 outgoing arrow.
    /// This non-standard representation gives much flexibility at converting regexes to NFA and
    /// preserves crucial information that can be used later for optimization purposes.
    /// </summary>
    public class State
    {
        private State? _next2;

        public CharClass? Match { get; private init; }
        public State? Next1 { get; set; }
        /// <summary>
        /// Alternative next state that only an ε-state may have.
        /// Check if this state is ε before write.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown by set if this state is non-ε.</exception> 
        public State? Next2
        {
            get => _next2;
            set
            {
                if (!IsEpsilon)
                    throw new InvalidOperationException();
                _next2 = value;
            }
        }

        /// <summary>
        /// Regex parser adds extra intermediate ε-states when generates loops in NFA.
        /// They are marked with this flag.
        /// This flag has a potential usage in optimization of various algorithms.
        /// </summary>
        public bool Back { get; private init; }

        /// <summary>
        /// An index of the current state.
        /// Negative value (default) means no index was assigned.
        /// If automaton is indexed, then each index is unique and the indices set is [0..n-1].
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Automaton doesn't have any deadlocks, so final state is the state with no outgoing arrows.
        /// </summary>
        public bool IsFinal { get => Next1 == null && Next2 == null; }

        public bool IsEpsilon { get => Match == null; }

        private State(CharClass? match, State? next1, State? next2, bool back)
        {
            this.Match = match;
            this.Next1 = next1;
            this._next2 = next2;
            this.Back = back;
            this.Index = -1;
        }

        public static State MakeEpsilon(State? next1 = null, State? next2 = null, bool back = false)
            => new State(null, next1, next2, back);

        public static State MakeConsuming(CharClass condition, State? next1 = null)
            => new State(condition, next1, null, false);
    }

    /// <summary>
    /// NFA automaton with ε-states.
    /// Sink states should be ε.
    /// If indices are assigned to states, then States[i].Index == i.
    /// </summary>
    public record Automaton(State[] sources, IReadOnlyList<State> states) { }
}

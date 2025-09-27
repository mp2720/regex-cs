namespace Regex.NFA
{
    /// <summary>
    /// Range of 8-bit characters [start..end].
    /// </summary>
    public record CharRange(char Start, char End) { }

    public record CharClass(IReadOnlyList<CharRange> Ranges, bool Inverted = false)
    {
        public static CharClass All()
            => new([], true);

        public static CharClass SingleChar(char c)
            => new([new(c, c)]);

        public static CharClass List(params char[] chs)
        {
            var ranges = new List<CharRange>();
            foreach (char c in chs)
                ranges.Add(new CharRange(c, c));
            return new CharClass(ranges);
        }

        /// <summary>
        /// Make an inverted copy.
        /// </summary>
        public CharClass Invert()
            => new(Ranges, !Inverted);
    }

    /// <summary>
    /// There are two different types of NFA states: ε and non-ε.
    /// ε-state also may be a "back" state - intermediate state in a loop back path.
    /// This non-standard representation gives much flexibility at converting regexes to NFA and
    /// preserves crucial information that can be used later for optimization purposes.
    /// </summary>
    public class State
    {
        public CharClass? Condition { get; private init; }
        public List<State> Next { get; private init; }

        /// <summary>
        /// Regex parser adds extra intermediate ε-states when generates loops in NFA.
        /// They are marked with this flag.
        /// This flag has a potential usage in optimization of various algorithms.
        /// </summary>
        public bool Back { get; private init; }

        /// <summary>
        /// An index of the current state.
        /// Negative value (default) means no index was assigned.
        /// </summary>
        public int Index { get; set; } = -1;

        public bool IsSink { get => Next.Count() == 0; }

        public bool IsEpsilon { get => Condition == null; }

        public State(CharClass? condition, bool back, List<State> next)
        {
            Condition = condition;
            Back = back;
            Next = next;
        }

        public static State MakeEpsilon()
            => new(null, false, []);

        public static State MakeBack()
            => new(null, true, []);

        public static State MakeConsuming(CharClass condition)
            => new(condition, false, []);

        public void AddNext(params State[] states) => Next.AddRange(states);
    }

    /// <summary>
    /// NFA automaton with ε-states.
    /// There's only one sink state; it has not outgoing arrows and it's an ε-state.
    /// Source and sink states could not be a "back" states.
    /// For all i: States[i].Index == i.
    /// </summary>
    public record Automaton(IReadOnlyList<State> Sources, State Sink, IReadOnlyList<State> States) { }
}

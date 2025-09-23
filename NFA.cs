namespace Regex.NFA
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
    public record Trans(CharClass? Condition, State To) { }

    public class State
    {
        public int Index { get; init; }
        public List<Trans> Transitions { get; } = [];

        public State(int index)
        {
            Index = index;
        }

        public void Transition(CharClass condition, State to)
        {
            Transitions.Add(new Trans(condition, to));
        }

        /// <summary>
        /// Make epsilon transition.
        /// </summary>
        public void Transition(State to)
        {
            Transitions.Add(new Trans(null, to));
        }
    }

    public record Automaton(State Start, State End, IReadOnlyList<State> states) { }
}

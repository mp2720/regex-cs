namespace Regex.NFA
{
    /// <summary>
    /// Primitive Thompson-style NFA scanner. Each step transitions to all possible states are processed.
    /// Thus the runtime performance is O(m*n) (m - size of NFA, n - size of input).
    /// </summary>
    public class Scanner(Automaton nfa)
    {
        private class ScanState(State state, int byteIndex)
        {
            public readonly State state = state;
            public int byteIndex = byteIndex;
        }

        private readonly Automaton nfa = nfa;

        private static bool DoMatch(byte c, CharClass cl)
        {
            bool matchesRanges = cl.Ranges.Any(range => range.From <= c && c <= range.To);
            return matchesRanges == !cl.Inverted;
        }

        public bool Match(byte[] input)
        {
            var scanStates = new ScanState?[nfa.States.Count];
            var newScanStates = new ScanState?[nfa.States.Count];
            bool hasNewStates = true;

            scanStates[nfa.Source.Index] = new ScanState(nfa.Source, 0);

            while (hasNewStates)
            {
                hasNewStates = false;
                for (int i = 0; i < scanStates.Length; ++i)
                {
                    var ss = scanStates[i];
                    if (ss == null)
                        continue;

                    scanStates[i] = null;

                    if (ss.state.Index == nfa.Sink.Index)
                    {
                        if (ss.byteIndex == input.Length)
                            return true; // match!

                        continue; // we're in the sink, but there're unmatched characters
                    }

                    foreach (var trans in ss.state.Transitions)
                    {
                        if (trans.Condition != null)
                        {
                            // non-ε

                            if (ss.byteIndex == input.Length)
                                // no characters left but we are not in the sink
                                continue;

                            if (!DoMatch(input[ss.byteIndex], trans.Condition))
                                continue;

                            ss.byteIndex++;
                        }

                        newScanStates[trans.To.Index] = new ScanState(trans.To, ss.byteIndex);
                        hasNewStates = true;
                    }
                }

                (scanStates, newScanStates) = (newScanStates, scanStates);
            }

            return false;
        }
    }
}

using System.Diagnostics;

namespace Regex.NFA
{
    public static class Optimizer
    {
        private static void TraverseEpsilonPathsRec(
            List<State> traversedPathEndings,
            State state,
            HashSet<int> visited)
        {
            if (visited.Contains(state.Index))
                return;
            visited.Add(state.Index);

            // we don't need a buckle, but we want ε-reachable non-ε and sink
            if (!state.IsEpsilon || state.IsSink)
                traversedPathEndings.Add(state);

            if (state.IsEpsilon)
                foreach (var nextState in state.Next)
                    TraverseEpsilonPathsRec(traversedPathEndings, nextState, visited);
        }

        /// <summary>
        /// Find all paths O,e1,...,en,T, where O is the origin, e1,...,en - ε-states, T - non-ε or sink.
        /// </summary>
        private static void TraverseEpsilonPaths(
            List<State> traversedPathEndings,
            State state,
            HashSet<int> visited)
        {
            foreach (var next in state.Next)
                TraverseEpsilonPathsRec(traversedPathEndings, next, visited);
        }

        public static Automaton Optimize(Automaton nfa)
        {
            // Each optimized state has the same index that corresponding original state had.
            // Later optimized NFA will be reindexed.

            // Indexed by original NFA indices.
            // Null means state is optimized out or not processed yet.
            // Original source states are kept until the end to simplify the code.
            var statesOptTable = new State?[nfa.States.Count];

            // Waves contain original states.
            // Current is cleared at the end of each step, swapped with next.
            var currentWave = new List<State>(nfa.States.Count);
            var nextWave = new List<State>(nfa.States.Count);

            var epsilonTraverseVisited = new HashSet<int>(nfa.States.Count);

            foreach (var source in nfa.Sources)
            {
                statesOptTable[source.Index] = new(source.Condition, source.Back, []);
                currentWave.Add(source);
            }

            while (currentWave.Count != 0)
            {
                foreach (var state in currentWave)
                {
                    if (state.IsSink)
                        continue;

                    var pathEndings = new List<State>();
                    TraverseEpsilonPaths(pathEndings, state, epsilonTraverseVisited);
                    epsilonTraverseVisited.Clear();
                    foreach (State pathEnd in pathEndings)
                    {
                        // make an optimized state for pathEnd, if there's no
                        // add it to the next wave
                        var pathEndOpt = statesOptTable[pathEnd.Index];
                        if (pathEndOpt == null)
                        {
                            pathEndOpt = statesOptTable[pathEnd.Index]
                                = new(pathEnd.Condition, pathEnd.Back, []);
                            nextWave.Add(pathEnd);
                        }

                        // find an optimized state (it surely was added to optimized at this point)
                        var stateOpt = statesOptTable[state.Index];
                        Debug.Assert(stateOpt != null);

                        // make a transition from state to pathEnd
                        stateOpt.AddNext(pathEndOpt);
                    }
                }

                currentWave.Clear();
                (nextWave, currentWave) = (currentWave, nextWave);
            }

            var sourcesOpt = new List<State>();
            // remove redundant ε-sources
            foreach (var source in nfa.Sources)
            {
                var sourceOpt = statesOptTable[source.Index];
                Debug.Assert(sourceOpt != null);

                if (!sourceOpt.IsEpsilon)
                {
                    sourcesOpt.Add(sourceOpt);
                    continue;
                }

                foreach (var next in sourceOpt.Next)
                    sourcesOpt.Add(next);

                statesOptTable[source.Index] = null;
            }

            int indexOpt = 0;
            var statesOpt = new List<State>();
            foreach (var stateOpt in statesOptTable)
            {
                if (stateOpt == null)
                    continue;

                stateOpt.Index = indexOpt++;
                statesOpt.Add(stateOpt);
            }

            var sinkOpt = statesOptTable[nfa.Sink.Index];
            Debug.Assert(sinkOpt != null);

            return new(sourcesOpt, sinkOpt, statesOpt);
        }
    }
}

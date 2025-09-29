namespace Regex.NFA.DFA;

using System.Diagnostics;
using Regex.NFA;

internal static class Determinizer
{
    /// <summary>
    /// Each transition s1 --> s2 transforms into s1 --> e --> s2,
    /// where e is an ε-state.
    /// Each source --> s (except the accepting one, if presented) transforms into --> e --> s.
    /// Accepting state's index is swapped with largest index in the given NFA.
    /// Every other state from the old NFA has the same index after transformation.
    /// All new added states have index >= number of states in the given NFA.
    /// </summary>
    private static Automaton AddEpsilonTransStates(Automaton nfa)
    {
        var newStates = new List<State>();
        int newStateIndex = 0;
        foreach (var state in nfa.States)
        {
            var newState = new State(state.Condition, false, []);
            newState.Index = newStateIndex++;
            if (state.Accept)
                newState.Accept = true;
            newStates.Add(newState);
        }

        var eSources = new List<State>();
        foreach (var source in nfa.Sources)
        {
            if (source.Accept)
                continue;

            var eSource = State.MakeEpsilon();
            eSource.Index = newStateIndex++;
            eSource.AddNext(newStates[source.Index]);
            newStates.Add(eSource);
            eSources.Add(eSource);
        }

        foreach (var state in nfa.States)
        {
            for (int i = 0; i < state.Next.Count; ++i)
            {
                var eState = State.MakeEpsilon();
                eState.Index = newStateIndex++;
                newStates.Add(eState);

                eState.AddNext(newStates[state.Next[i].Index]);
                newStates[state.Index].AddNext(eState);
            }
        }

        // // swap last original state with the accepting state,
        // // so epsilon states are contiguos in the array.
        // int oldAcceptIndex = nfa.Accept.Index;
        // int newAcceptIndex = nfa.States.Count - 1;
        // newStates[oldAcceptIndex].Index = newAcceptIndex;
        // newStates[newAcceptIndex].Index = oldAcceptIndex;
        // (newStates[oldAcceptIndex], newStates[newAcceptIndex]) = (newStates[newAcceptIndex], newStates[oldAcceptIndex]);

        return new(eSources, newStates[nfa.Accept.Index], newStates);
    }

    private static HashableBitArray MakeDState(List<State> eStates, int epsilonCount, int othersCount)
    {
        var bits = new HashableBitArray(epsilonCount);
        foreach (var state in eStates)
        {
            int bit = state.Index - othersCount;
            Debug.Assert(bit >= 0);
            bits.Set(bit);
        }
        return bits;
    }

    public static Automaton MakeDFA(Automaton nfa, IEnumerable<byte> alphabet)
    {
        var nfa2 = AddEpsilonTransStates(nfa);
        // Let G be such a graph that:
        //  V(G) = { s in V(nfa2) | s is epsilon or s accepts }
        //  E(G) = { e1->e2 | path e1->s->e2 is in nfa2 }
        //
        // Let's define a transition function for arrows:
        //  δ(e1, e2) = set of characters s consumed ∀e1,s,e2 such that path e1->s->e2 is in nfa2.
        //
        // Now let's say A is NFA with the sources and accepting state being the same as in nfa2.
        //
        // A is equivalent in terms of language it recognizes to the given nfa. 
        // But it also has a standard representation, not the optimized one we're always using.
        // That's how the following algorithm sees the given nfa.

        // return nfa2;

        var nonEpsCount = nfa.States.Count;
        var epsCount = nfa2.States.Count - nonEpsCount;

        var dStates = new List<State>();
        var dEpsStates = new Dictionary<HashableBitArray, State>();
        var unmarkedDEpsStates1 = new Stack<(HashableBitArray mask, State st)>();
        var unmarkedDEpsStates2 = new Stack<(HashableBitArray mask, State st)>();
        var dSources = new List<State>(nfa2.Sources.Count);
        var acceptingStates = new List<State>();

        foreach (var src in nfa2.Sources)
        {
            var mask = new HashableBitArray(epsCount, src.Index - nonEpsCount);
            var dSource = State.MakeEpsilon();
            dEpsStates.Add(mask, dSource);
            dStates.Add(dSource);
            unmarkedDEpsStates1.Push((mask, dSource));
            dSources.Add(dSource);
        }

        while (unmarkedDEpsStates1.Count != 0)
        {
            foreach (var (dStateMask, dState) in unmarkedDEpsStates1)
            {
                bool reachedAccept = false;

                foreach (byte c in alphabet)
                {
                    var nextDStateMask = new HashableBitArray(epsCount);
                    bool emptySet = true;

                    foreach (var i in dStateMask.GetOnesIndices())
                    {
                        var epsilonState = nfa2.States[nonEpsCount + i];
                        Debug.Assert(epsilonState.Next.Count == 1);

                        var consumingState = epsilonState.Next[0];
                        if (consumingState.Accept)
                        {
                            reachedAccept = true;
                            // emptySet = false;
                            continue;
                        }

                        Debug.Assert(consumingState.Condition != null);
                        if (!consumingState.Condition.Matches(c))
                            continue;

                        foreach (var nextEpsilonState in consumingState.Next)
                        {
                            emptySet = false;
                            nextDStateMask.Set(nextEpsilonState.Index - nonEpsCount);
                        }
                    }

                    if (emptySet)
                        continue;

                    State nextDState;
                    if (dEpsStates.ContainsKey(nextDStateMask))
                    {
                        nextDState = dEpsStates[nextDStateMask];
                    }
                    else
                    {
                        nextDState = State.MakeEpsilon();
                        dEpsStates.Add(nextDStateMask, nextDState);
                        dStates.Add(nextDState);
                        unmarkedDEpsStates2.Push((nextDStateMask, nextDState));
                    }

                    // if (acceptingState)
                    //     acceptingStates.Add(nextDState);

                    var consumingDState = State.MakeConsuming(CharClass.SingleChar(c));
                    dStates.Add(consumingDState);
                    dState.AddNext(consumingDState);
                    consumingDState.AddNext(nextDState);

                    // dStates.Add(nextDStateMask, nextDState);
                    // unmarkedDStates2.Push((nextDStateMask, nextDState));
                }

                if (reachedAccept)
                    acceptingStates.Add(dState);

            }

            unmarkedDEpsStates1.Clear();
            (unmarkedDEpsStates1, unmarkedDEpsStates2) = (unmarkedDEpsStates2, unmarkedDEpsStates1);
        }

        // Follow the rule that there's only one accepting state.
        var dAcceptState = State.MakeEpsilon();
        dStates.Add(dAcceptState);
        dAcceptState.Accept = true;
        foreach (var dState in acceptingStates)
            dState.AddNext(dAcceptState);

        var dfa = new Automaton(dSources, dAcceptState, dStates);
        dfa.AssignIndices();
        
        return dfa;
    }
}


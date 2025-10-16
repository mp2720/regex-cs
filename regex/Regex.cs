using System.Diagnostics;
using System.Runtime.InteropServices;
using Regex.Parser;
using Regex.Runtime;

namespace Regex
{
    public class CompiledRegex : IDisposable
    {
        private bool disposed = false;

        // These pointers to arrays are saved in order to free them later.
        private unsafe readonly NativeAPI.CharRange* charRangesArr;
        private unsafe readonly NativeAPI.State* statesArr;
        private unsafe readonly NativeAPI.Automaton* nativeNFA;

        // Pointer to scanner and it's pinned handle
        private readonly IntPtr scannerPtr;

        private static string errorToString(NativeAPI.Error err)
        {
            IntPtr strPtr = NativeAPI.rcs_strerror(err);
            string? s = Marshal.PtrToStringUTF8(strPtr);
            Debug.Assert(s != null);
            return s;
        }

        public unsafe CompiledRegex(string regex)
        {
            var nfa = RegexParser.WithDefaultBuiltinClasses().Convert(regex);
            nfa = NFA.Optimizer.Optimize(nfa);

            // Count number of ranges so we can allocate a single ranges array
            int charRangesCount = 0;
            foreach (var state in nfa.States)
                if (state.Condition != null)
                    charRangesCount += state.Condition.Ranges.Count();

            charRangesArr = (NativeAPI.CharRange*)Marshal
                .AllocHGlobal(charRangesCount * sizeof(NativeAPI.CharRange)).ToPointer();

            statesArr = (NativeAPI.State*)Marshal
                .AllocHGlobal(nfa.States.Count * sizeof(NativeAPI.State)).ToPointer();

            // marshal each state to a native struct
            int stateIndex = 0;
            int rangeIndex = 0;
            foreach (var state in nfa.States)
            {
                // retreive all data from nullable condition
                NativeAPI.CharRange* stateRangesPtr = null; // use null for clarity
                int rangesCount = 0;
                bool inverted = false;
                if (state.Condition != null)
                {
                    stateRangesPtr = &charRangesArr[rangeIndex];
                    rangesCount = state.Condition.Ranges.Count;
                    inverted = state.Condition.Inverted;

                    foreach (var range in state.Condition.Ranges)
                        charRangesArr[rangeIndex++] = new() { start = range.Start, end = range.End };
                }

                // make an array of pointers to next states
                NativeAPI.State** nextStates = (NativeAPI.State**)Marshal
                    .AllocHGlobal(state.Next.Count * sizeof(NativeAPI.State*)).ToPointer();
                int nextStateIndex = 0;
                foreach (var nextState in state.Next)
                    nextStates[nextStateIndex++] = statesArr + nextState.Index;

                // write a state itself
                statesArr[stateIndex++] = new()
                {
                    next = new IntPtr(nextStates),
                    nextLen = (uint)state.Next.Count,
                    ranges = new IntPtr(stateRangesPtr),
                    rangesLen = (uint)rangesCount,
                    invertedMatch = (byte)(inverted ? 1 : 0)
                };
            }

            nativeNFA = (NativeAPI.Automaton*)Marshal
                .AllocHGlobal(sizeof(NativeAPI.Automaton)).ToPointer();

            // make an array of pointers to source states
            var sourceStates = (NativeAPI.State**)Marshal
                .AllocHGlobal(nfa.Sources.Count * sizeof(NativeAPI.State)).ToPointer();
            int sourceStateIndex = 0;
            foreach (var source in nfa.Sources)
                sourceStates[sourceStateIndex++] = &statesArr[source.Index];

            *nativeNFA = new()
            {
                states = new IntPtr(statesArr),
                statesLen = (uint)nfa.States.Count(),
                sourceStates = new IntPtr(sourceStates),
                sourceStatesLen = (uint)nfa.Sources.Count,
                acceptState = new IntPtr(&statesArr[nfa.Accept.Index])
            };

            var err = NativeAPI.rcs_scanner_init(out scannerPtr, new IntPtr(nativeNFA));
            if (!err.Ok())
                throw new NativeAPIException(errorToString(err));
        }

        public unsafe bool Match(Reader inputReader)
        {
            byte ok = 0;
            inputReader.Exception = null;
            var err = NativeAPI.rcs_match(out ok, scannerPtr, new IntPtr(inputReader.Native));
            if (!err.Ok())
                throw new NativeAPIException(errorToString(err));
            if (inputReader.Exception != null)
                throw inputReader.Exception;
            return ok != 0;
        }

        public bool Match(byte[] bytes)
        {
            return Match(new ByteArrayReader(bytes));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public unsafe void Dispose(bool disposing)
        {
            if (!disposed)
            {
                for (int i = 0; i < nativeNFA->statesLen; ++i)
                    Marshal.FreeHGlobal(statesArr[i].next);
                Marshal.FreeHGlobal(new IntPtr(statesArr));

                Marshal.FreeHGlobal(nativeNFA->sourceStates);
                Marshal.FreeHGlobal(new IntPtr(charRangesArr));

                Marshal.FreeHGlobal(new IntPtr(nativeNFA));

                NativeAPI.rcs_scanner_free(scannerPtr);

                disposed = true;
            }
        }
    }
}

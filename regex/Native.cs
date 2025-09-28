using System.Runtime.InteropServices;

namespace Regex
{
    public class NativeAPIException : Exception
    {
        public NativeAPIException(string reason) : base(reason) { }
    }

    internal static partial class NativeAPI
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Error
        {
            public uint code; // uint32_t
            public int libcErrno; // rcs_api_libc_errno

            public bool Ok()
            {
                return code == 0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CharRange
        {
            public byte start; // uint8_t
            public byte end; // uint8_t
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct State
        {
            public IntPtr next; // struct rcs_nfa_state*
            public uint nextLen; // rcs_api_size
            public IntPtr ranges; // struct rcs_nfa_char_range*
            public uint rangesLen; // rcs_api_size
            public byte invertedMatch; //rcs_api_bool
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct Automaton
        {
            public IntPtr states; // struct rcs_nfa_state*
            public uint statesLen; // rcs_api_size
            public IntPtr sourceStates; // struct rcs_nfa_state*
            public uint sourceStatesLen; // rcs_api_size
            public IntPtr sinkState; // struct rcs_nfa_state*
        };

        [LibraryImport("libregex-cs-runtime.so")]
        public static partial Error rcs_init_scanner(IntPtr scannerOut, IntPtr nfa);

        [LibraryImport("libregex-cs-runtime.so")]
        public static partial Error rcs_debug_dump_nfa(IntPtr textOut, IntPtr nfa);

        [LibraryImport("libregex-cs-runtime.so", StringMarshalling = StringMarshalling.Utf8)]
        public static partial String rcs_strerror(Error err);
    }
}

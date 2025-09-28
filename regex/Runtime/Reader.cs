using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Regex.Runtime
{
    /// <summary>
    /// Parent of all input readers used by scanners.
    /// Allocates a POH buffer that is both accessible for native and managed code.
    /// Reader should be ready to handle any number of failing Read() and Unwind() calls.
    /// It may throw an exception, but it will be handled (only the first one) after the scanning
    /// process is finished.
    /// </summary>
    public abstract class Reader : IDisposable
    {
        /// <summary>
        /// Only bytes in range [M..M+N) contain the data read by last Read() call.
        /// Others are garbage.
        /// (M, N) is a return value of the last Read() call.
        /// </summary>
        public byte[] Buffer { get; private init; }

        // Pointer to start of the Buffer. Saved here to not write 'fixed()' everywhere.
        // It's address is persistent since Buffer is allocated in POH.
        private unsafe byte* bufferPtr;

        /// <summary>
        /// Exception catched when the reader was called from native code.
        /// Set to null before the scanner routine call.
        /// </summary>
        internal Exception? Exception { get; set; } = null;

        /// <summary>
        /// WARNING: do not leak this structure to the context where the reader might be destroyed by GC,
        /// because this struct holds addresses of .NET JIT-generated callbacks which are referring to this
        /// object.
        /// Freed by Dispose().
        /// </summary>
        internal unsafe NativeAPI.Reader* Native { get; private init; }
        private bool disposed = false;

        public Reader(int bufferCapacity)
        {
            unsafe
            {
                Buffer = GC.AllocateUninitializedArray<byte>(bufferCapacity, pinned: true);
                fixed (byte* ptr = Buffer)
                {
                    bufferPtr = ptr;
                }

                Native = (NativeAPI.Reader*)Marshal.AllocHGlobal(sizeof(NativeAPI.Reader));
                Native->read = Marshal.GetFunctionPointerForDelegate<NativeAPI.Read>(ReadNative);
                Native->unwind = Marshal.GetFunctionPointerForDelegate<NativeAPI.Unwind>(UnwindNative);
                Native->buf = new IntPtr(bufferPtr);
                // .NET just JIT-generated functions with hardcoded 'this' address for us.
                // So the arg is not required.
                Native->arg = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Fill the reader's buffer and advance.
        /// </summary>
        /// <returns>
        /// Returns an index and size of the buffer slice.
        /// This slice contains read bytes.
        /// </returns>
        public abstract (uint sliceStart, uint sliceLen) Read();

        /// <summary>
        /// Go back n bytes.
        /// </summary>
        public abstract void Unwind(ulong n);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private R HandleExceptionsWhenCalledFromUnmanaged<R>(Func<R> f, R onFail)
        {
            try
            {
                return f();
            }
            catch (Exception e)
            {
                if (this.Exception != null)
                    this.Exception = e;
                return onFail;
            }
        }

        private uint ReadNative(IntPtr arg)
            => HandleExceptionsWhenCalledFromUnmanaged<uint>(() =>
            {
                var (sliceStart, sliceLen) = Read();
                unsafe
                {
                    Native->buf = new IntPtr(bufferPtr + sliceStart);
                }
                return sliceLen;
            }, 0);

        private byte UnwindNative(IntPtr arg, ulong n)
            => HandleExceptionsWhenCalledFromUnmanaged<byte>(
                () => { Unwind(n); return 1; }, 0
            );

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public unsafe void Dispose(bool disposing)
        {
            if (!disposed)
            {
                Marshal.FreeHGlobal(new IntPtr(Native));
                disposed = true;
            }
        }
    }

    public class ByteArrayReader : Reader
    {
        private uint sliceStart = 0;

        public ByteArrayReader(byte[] arr) : base(arr.Length)
        {
            arr.CopyTo(Buffer, 0);
        }

        public override (uint sliceStart, uint sliceLen) Read()
        {
            var ret = (sliceStart, (uint)(Buffer.Length - sliceStart));
            sliceStart = (uint)Buffer.Length;
            return ret;
        }

        public override void Unwind(ulong n)
        {
            if (n > sliceStart)
                throw new InvalidOperationException("Could not unwind more characters than were read");
            sliceStart -= (uint)n;
        }
    }
}

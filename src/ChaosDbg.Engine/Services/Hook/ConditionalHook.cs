using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ChaosLib;
using ClrDebug;
using Iced.Intel;

namespace ChaosDbg.Hook
{
    /// <summary>
    /// Represents a hook that consists of a native trampoline that then dispatches to managed code when a certain condition is met.<para/>
    /// This type is used when a straight managed hook cannot be used, e.g. because the function to be hooked is also called from DllMain (which
    /// runs under the loader lock, and managed code cannot be called into from under the loader lock)
    /// </summary>
    abstract class ConditionalHook<TDelegate>
    {
        public IntPtr pTrampoline { get; protected set; }

        protected IntPtr pOriginal;
        private TDelegate hookDelegate; //Prevent the delegate from being GC'd
        protected IntPtr pHook;

        public bool Hooked { get; set; }

        protected ConditionalHook(IntPtr pOriginal, TDelegate hookDelegate)
        {
            this.pOriginal = pOriginal;
            this.hookDelegate = hookDelegate;
            pHook = Marshal.GetFunctionPointerForDelegate(hookDelegate);
        }

        ~ConditionalHook()
        {
            if (Hooked)
                Debug.Assert(false, $"{GetType().Name} was GC'd before it was safe to do so. References to hook delegates must be retained until they are removed, or else native code will crash when it attempts to call into them");
        }

        protected void CreateTrampoline(Assembler c, int expectedSize)
        {
            //Assemble
            var stream = new MemoryStream();

            //Since our jumps are short, the IP we specify when we assemble our instructions doesn't matter. But when it comes to calls, the source instruction does matter.
            //Instructions don't have a length until they've been disassembled, which is a bit of a problem as we don't know how much space we need to allocate. It's a catch-22.
            //As such, we've hardcoded various sizes for various configurations, and assert that we correctly guessed the buffer size needed
            c.Assemble(new StreamCodeWriter(stream), (ulong) pTrampoline);

            Marshal.Copy(stream.GetBuffer(), 0, pTrampoline, (int) stream.Length);

            //For some reason, sometimes the actual size of the assembled instructions can vary a bit depending on whether we're debugging or not. As such,
            //we just ensure we've allocated a buffer big enough to store everything
            if (stream.Length >= expectedSize)
                throw new InvalidOperationException($"Assembled instructions were {stream.Length} bytes, however the expected length is supposed to be {expectedSize} bytes");

            //Trampoline has been written, remove Write permission from buffer
            Kernel32.VirtualProtect(pTrampoline, (IntPtr) stream.Length, MemoryProtection.ExecuteRead);
            Hooked = true;
        }

        protected IntPtr AllocateTrampoline(int size) =>
            Kernel32.VirtualAllocEx(Kernel32.GetCurrentProcess(), size, MEM_TYPE_FLAGS.MEM_COMMIT | MEM_TYPE_FLAGS.MEM_RESERVE, MemoryProtection.ExecuteReadWrite);

        public TDelegate GetDelegate() => Marshal.GetDelegateForFunctionPointer<TDelegate>(pTrampoline);

        public virtual void Dispose()
        {
            if (pTrampoline != IntPtr.Zero)
            {
                Kernel32.VirtualFreeEx(Kernel32.GetCurrentProcess(), pTrampoline);
                pTrampoline = IntPtr.Zero;
            }
        }
    }
}

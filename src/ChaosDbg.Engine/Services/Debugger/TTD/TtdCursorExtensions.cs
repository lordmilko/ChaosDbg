using System;
using ClrDebug;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    static class TtdCursorExtensions
    {
        public static unsafe CrossPlatformContext GetCrossPlatformContext(this Cursor cursor, ThreadId threadId = default)
        {
            using var buffer = new ChaosLib.MemoryBuffer(0xA70);

            cursor.GetCrossPlatformContext(buffer, threadId);

            var context = new CrossPlatformContext(ClrDebug.ContextFlags.AMD64ContextAll, *(CROSS_PLATFORM_CONTEXT*) (IntPtr) buffer);

            return context;
        }

        public static ThreadInfo GetThreadInfo(this Cursor cursor) =>
            cursor.GetThreadInfo(ThreadId.Active);

        public static Position GetPosition(this Cursor cursor) =>
            cursor.GetPosition(ThreadId.Active);
    }
}

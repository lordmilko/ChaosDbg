#if DEBUG
using System;
using System.Diagnostics;
using System.Text;
using ChaosLib;
using ChaosLib.Detour;
using ClrDebug;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    /// <summary>
    /// Provides facilities for hooking and tracing API calls made by TTD.
    /// </summary>
    static class TTDTracer
    {
        public static void Hook(ReplayEngine initial)
        {
            _ = new RawVtblHook(initial, ReplayEngineHook);
        }

        static unsafe object ReplayEngineHook(DetourContext ctx)
        {
            switch (ctx.Name)
            {
                case "RegisterDebugModeAndLogging":
                case "GetThreadCount":
                case "GetIndexStatus":
                case "GetModuleLoadedEventCount":
                case "UnsafeAsInterface":
                case "GetModuleUnloadedEventCount":
                case "GetThreadCreatedEventCount":
                case "GetThreadTerminatedEventCount":
                case "GetExceptionEventCount":
                case "GetRecordClientCount":
                    return LogDefault("ReplayEngine", ctx);

                case "GetSystemInfo":
                {
                    Debug.WriteLine("ReplayEngine::" + ctx);

                    var result = ctx.InvokeOriginal();

                    return result;
                }

                case "NewCursor":
                {
                    Debug.WriteLine("ReplayEngine::" + ctx);

                    var result = ctx.InvokeOriginal();

                    _ = new RawVtblHook(new Cursor((IntPtr) result), CursorHook);

                    return result;
                }

                case "GetThreadList":
                {
                    var threadCount = new ReplayEngine(ctx.Arg<IntPtr>("this")).ThreadCount;

                    var result = ctx.InvokeOriginal();

                    ThreadInfo* threads = (ThreadInfo*) (IntPtr) result;

                    for (var i = 0; i < threadCount; i++)
                    {
                        var thread = threads[i];

                        Debug.WriteLine($"    index = {thread.UniqueThreadId}, threadId = {thread.ThreadId}");
                    }

                    return result;
                }

                case "GetThreadInfo":
                {
                    var result = ctx.InvokeOriginal();

                    var threadInfo = (ThreadInfo*) (IntPtr) result;

                    Debug.WriteLine("ReplayEngine::" + ctx + $" -> (index = {threadInfo->UniqueThreadId}, threadId = {threadInfo->ThreadId})");

                    return result;
                }

                case "GetModuleLoadedEventList":
                {
                    //temp:
                    return ctx.InvokeOriginal();
                }

                case "GetModuleUnloadedEventList":
                {
                    //temp:
                    return ctx.InvokeOriginal();
                }

                case "GetThreadCreatedEventList":
                {
                    //temp:
                    return ctx.InvokeOriginal();
                }

                case "GetThreadTerminatedEventList":
                {
                    //temp:
                    return ctx.InvokeOriginal();
                }

                case "GetExceptionEventList":
                {
                    var exceptionCount = new ReplayEngine(ctx.Arg<IntPtr>("this")).ExceptionEventCount;

                    var result = ctx.InvokeOriginal();

                    ExceptionEvent* exceptions = (ExceptionEvent*) (IntPtr) result;

                    for (var i = 0; i < exceptionCount; i++)
                    {
                        var exception = exceptions[i];

                        //Debug.WriteLine($"    index = {thread.index}, threadId = {thread.threadId}");
                    }

                    return result;
                }

                case "GetRecordClientList":
                {
                    //temp:
                    return ctx.InvokeOriginal();
                }

                case "BuildIndex":
                {
                    Debug.Write("ReplayEngine::" + ctx);

                    var @this = ctx.Arg<IntPtr>("this");
                    var callback = ctx.Arg<BuildIndexCallback>("callback");
                    var context = ctx.Arg<IntPtr>("context");
                    //var unk1 = (IntPtr) 1;
                    var unk2 = ctx.Arg<IndexBuildFlags>("flags");

                    BuildIndexCallback myCallback = (a, b) =>
                    {
                        Debug.WriteLine($"    Callback: a = {a}, totalKeyframes = {b->totalKeyframes}, completedKeyframe = {b->completedKeyframe}");
                        callback(a, b);
                    };

                    var result = ctx.InvokeOriginal(@this, myCallback, context, unk2);

                    return result;
                }

                case "GetFirstPosition":
                case "GetLastPosition":
                {
                    var result = ctx.InvokeOriginal();

                    var position = (Position*) (IntPtr) result;

                    Debug.Write("ReplayEngine::" + ctx + " -> " + *position);

                    return result;
                }

                default:
                    throw new NotImplementedException($"Don't know how to handle function {ctx.Name}");
            }
        }

        static unsafe object CursorHook(DetourContext ctx)
        {
            switch (ctx.Name)
            {
                case "SetPosition":
                case "GetThreadCount":
                case "GetTebAddress":
                case "GetAvxExtendedContext":
                case "GetCrossPlatformContext":
                case "GetModuleCount":
                case "SetEventMask":
                case "UnsafeAsInterface":
                case "GetDefaultMemoryPolicy":
                case "GetProgramCounter":
                case "GetStackPointer":
                case "SetPositionOnThread":
                case "SetReplayFlags":
                    return LogDefault("Cursor", ctx);

                case "GetThreadList":
                {
                    Debug.WriteLine("Cursor::" + ctx);

                    var threadCount = new Cursor(ctx.Arg<IntPtr>("this")).ThreadCount;

                    var result = ctx.InvokeOriginal();

                    ActiveThreadInfo* threads = (ActiveThreadInfo*) (IntPtr) result;

                    for (var i = 0; i < threadCount; i++)
                    {
                        var thread = threads[i];

                        Debug.WriteLine($"    index = {thread.info->UniqueThreadId}, threadId = {thread.info->ThreadId}, next = {thread.next}, last = {thread.last}");
                    }

                    return result;
                }

                case "GetThreadInfo":
                {
                    var result = ctx.InvokeOriginal();

                    var threadInfo = (ThreadInfo*) (IntPtr) result;

                    Debug.WriteLine("Cursor::" + ctx + $" -> (index = {threadInfo->UniqueThreadId}, threadId = {threadInfo->ThreadId})");

                    return result;
                }

                case "QueryMemoryBuffer":
                {
                    var result = ctx.InvokeOriginal();

                    var memoryBuffer = (ClrDebug.TTD.MemoryBuffer*) (IntPtr) result;

                    //Debug.WriteLine("Cursor::" + ctx + " -> size: " + memoryBuffer->size);

                    return result;
                }

                case "GetModuleList":
                {
                    Debug.WriteLine("Cursor::" + ctx);

                    var moduleCount = new Cursor(ctx.Arg<IntPtr>("this")).ModuleCount;

                    var result = ctx.InvokeOriginal();

                    ModuleInstance* modules = (ModuleInstance*) (IntPtr) result;

                    for (var i = 0; i < moduleCount; i++)
                    {
                        var module = modules[i];

                        Debug.WriteLine($"    module = {*module.Module}, base = {module.Module->address}, FirstSequence = {module.FirstSequence}, LastSequence = {module.LastSequence}");
                    }

                    return result;
                }

                case "GetPosition":
                {
                    var result = ctx.InvokeOriginal();

                    var position = (Position*) (IntPtr) result;

                    Debug.WriteLine("Cursor::" + ctx + " -> " + *position);

                    return result;
                }

                case "ReplayForward":
                case "ReplayBackward":
                {
                    var @this = ctx.Arg<IntPtr>("this");
                    var replayResult = (ReplayResult*) ctx.Arg<IntPtr>("replayResult");
                    var initialResult = *replayResult;

                    var endPos = (Position*) ctx.Arg<IntPtr>("replayUntil");
                    var initialEndPos = *endPos;

                    var stepCount = ctx.Arg<StepCount>("stepCount");

                    Debug.WriteLine($"    {ctx.Name}...");
                    var result = ctx.InvokeOriginal();

                    var endResult = *replayResult;
                    var endEndPos = *endPos;

                    var builder = new StringBuilder();

                    builder.Append("Cursor::[").Append(ctx.Name).Append("] ");

                    Debug.Assert(initialEndPos.Equals(endEndPos));

                    var fields = typeof(ReplayResult).GetFields();

                    builder.Append("ReplayResult { ");

                    for (var i = 0; i < fields.Length; i++)
                    {
                        var field = fields[i];

                        var first = field.GetValue(initialResult);
                        var second = field.GetValue(endResult);

                        string Format(object value)
                        {
                            if (field.FieldType.IsEnum)
                                return value.ToString();

                            var typeCode = Type.GetTypeCode(field.FieldType);

                            switch (typeCode)
                            {
                                case TypeCode.Byte:
                                    return "0x" + ((byte) value).ToString("X");

                                case TypeCode.Int16:
                                    return "0x" + ((short) value).ToString("X");

                                    case TypeCode.Int64:
                                    return "0x" + ((long) value).ToString("X");

                                default:
                                    throw new UnknownEnumValueException(typeCode);
                            }
                        }

                        builder.Append(field.Name).Append(" = ").Append(Format(second));

                        if (i < fields.Length - 1)
                            builder.Append(", ");
                    }

                    builder.Append(" } ");

                    builder.Append($", endPos = {initialEndPos}");
                    builder.Append(", stepCount = ").Append(stepCount);

                    Debug.WriteLine(builder.ToString());

                    return result;
                }

                case "AddMemoryWatchpoint":
                case "RemoveMemoryWatchpoint":
                {
                    var @this = ctx.Arg<IntPtr>("this");
                    var data = (MemoryWatchpointData*) ctx.Arg<IntPtr>("memoryWatchpointData");

                    var result = ctx.InvokeOriginal();

                    Debug.WriteLine("Cursor::" + ctx.Name + " -> " + result + $" (address = {data->address}, flags = {data->flags}, size = {data->size})");

                    return result;
                }

                case "SetMemoryWatchpointCallback":
                {
                    var @this = ctx.Arg<IntPtr>("this");
                    var callback = ctx.Arg<MemoryWatchpointCallbackRaw>("callback");
                    var unknown = ctx.Arg<long>("context");

                    MemoryWatchpointCallbackRaw cb = (a, b, c) =>
                    {
                        var tv = new ThreadView(c);

                        var hook = new RawVtblHook(tv, ThreadViewHook);

                        var cbRes = WithThreadView(() => callback(a, b, c));

                        Debug.WriteLine($"    MemoryWatchpointCallback: context = {a}, address = {b->address}, position = {tv.Position}, flags = {b->flags}, size = {b->size}, returnValue = {cbRes}");

                        return cbRes;
                    };

                    Debug.WriteLine("Cursor::" + ctx);

                    return ctx.InvokeOriginal(@this, cb, unknown);
                }

                case "SetRegisterChangedCallback":
                {
                    //temp
                    return LogDefault("Cursor", ctx);
                }

                case "SetThreadContinuityBreakCallback":
                {
                    //temp
                    return LogDefault("Cursor", ctx);
                }

                case "SetReplayProgressCallback":
                {
                    var @this = ctx.Arg<IntPtr>("this");
                    var callback = ctx.Arg<ReplayProgressCallback>("callback");
                    var unknown = ctx.Arg<long>("context");

                    ReplayProgressCallback cb = (a, b) =>
                    {
                        WithThreadView(() => callback(a, b));
                    };

                    Debug.WriteLine("Cursor::" + ctx);

                    return ctx.InvokeOriginal(@this, cb, unknown);
                }

                case "QueryMemoryRange":
                {
                    //temp
                    return LogDefault("Cursor", ctx);
                }

                default:
                    throw new NotImplementedException($"Don't know how to handle function {ctx.Name}");
            }
        }

        [ThreadStatic]
        private static bool logThreadView;

        private static void WithThreadView(Action action)
        {
            //Disable calls made by TTDReplay against the IThreadView that have nothing to do with calls actually being made in a callback
            logThreadView = true;

            try
            {
                action();
            }
            finally
            {
                logThreadView = false;
            }
        }

        private static T WithThreadView<T>(Func<T> action)
        {
            logThreadView = true;

            try
            {
                return action();
            }
            finally
            {
                logThreadView = false;
            }
        }

        static unsafe object ThreadViewHook(DetourContext ctx)
        {
            if (!logThreadView)
                return ctx.InvokeOriginal();

            switch (ctx.Name)
            {
                case "GetStackPointer":
                case "GetCrossPlatformContext":
                case "GetProgramCounter":
                    return LogDefault($"[{Kernel32.GetCurrentThreadId()}] ThreadView", ctx);

                case "GetPosition":
                case "GetPreviousPosition":
                {
                    var result = ctx.InvokeOriginal();

                    var position = (Position*) (IntPtr) result;

                    Debug.WriteLine($"[{Kernel32.GetCurrentThreadId()}] ThreadView::" + ctx + " -> " + *position);

                    return result;
                }

                case "QueryMemoryBuffer":
                {
                    var result = ctx.InvokeOriginal();

                    var memoryBuffer = (ClrDebug.TTD.MemoryBuffer*) (IntPtr) result;

                    //Debug.WriteLine("ThreadView::" + ctx + " -> size: " + memoryBuffer->size);

                    if (memoryBuffer->size == 0)
                    {
                        var x = 0;
                    }

                    return result;
                }

                case "GetThreadInfo":
                {
                    var result = ctx.InvokeOriginal();

                    var threadInfo = (ThreadInfo*) (IntPtr) result;

                    Debug.WriteLine($"[{Kernel32.GetCurrentThreadId()}] ThreadView::" + ctx + $" -> (index = {threadInfo->UniqueThreadId}, threadId = {threadInfo->ThreadId})");

                    return result;
                }

                default:
                    throw new NotImplementedException($"Don't know how to handle function {ctx.Name}");
            }
        }

        private static object LogDefault(string className, DetourContext ctx)
        {
            var result = ctx.InvokeOriginal();

            Debug.WriteLine(className + "::" + ctx + " -> " + result);

            return result;
        }
    }
}
#endif

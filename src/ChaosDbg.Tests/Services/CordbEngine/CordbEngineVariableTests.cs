using System.Diagnostics;
using System.Linq;
using ChaosDbg.Cordb;
using ChaosLib;
using ChaosLib.TypedData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    public class CordbEngineVariableTests : BaseTest
    {
        [TestMethod]
        public void CordbEngine_Variables_Native_FirstFrame_Unestablished()
        {
            //Before the frame has even been fully established, should we be able to see our locals properly?

            TestNativeMain(
                ctx =>
                {
                    var locals = ctx.CurrentFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreEqual(0, ctx.CurrentFrame.Context.BP);

                    //In x64, WinDbg also fails to get the locals at the very beginning of the frame, because the frame's BP hasn't
                    //been established yet and is 0
                    Assert.AreEqual(3, locals.Length);
                    Assert.IsTrue(locals.Any(l => l.Value is DbgRemoteBadValue));
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Variables_Native_SecondFrame_Unestablished()
        {
            //Before the frame has even been fully established, we should be able to see our locals properly

            TestNativeMain(
                ctx =>
                {
                    ctx.MoveTo("BindLifetimeToParentProcess");

                    var locals = ctx.CurrentFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreNotEqual(0, ctx.CurrentFrame.Context.BP);

                    //It's just a buffer, so doesn't have a value yet
                    Assert.AreEqual(1, locals.Length);

                    //We should still be able to get the variables of the previous frame
                    var previousLocals = ctx.PreviousFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreEqual(3, previousLocals.Length);
                    Assert.IsTrue(previousLocals[0].Value.IsEquivalentTo(3));
                    Assert.IsTrue(previousLocals[1].Value.IsEquivalentTo(ctx.Process.CommandLine[0]));
                    Assert.IsTrue(previousLocals[2].Value.IsEquivalentTo(NativeTestType.Com));
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Variables_Native_FirstFrame_Established()
        {
            //Ensure we're not using the system32 version
            GetService<NativeLibraryProvider>().GetModuleHandle(WellKnownNativeLibrary.DbgHelp);

            //Before the frame has even been fully established, we should be able to see our locals properly

            TestNativeMain(
                ctx =>
                {
                    while (ctx.CurrentFrame.Context.BP == 0)
                        ctx.StepOver();

                    var locals = ctx.CurrentFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreEqual(3, locals.Length);

                    //NativeTestType hasn't been initialized yet, and contains junk
                    Assert.IsTrue(locals[0].Value.IsEquivalentTo(3));
                    Assert.IsTrue(locals[1].Value.IsEquivalentTo(ctx.Process.CommandLine[0]));
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Variables_Native_SecondFrame_Established()
        {
            //Before the frame has even been fully established, we should be able to see our locals properly

            TestNativeMain(
                ctx =>
                {
                    ctx.MoveTo("BindLifetimeToParentProcess");

                    var oldBP = ctx.CurrentFrame.Context.BP;

                    while (ctx.CurrentFrame.Context.BP == oldBP)
                        ctx.StepOver();

                    var locals = ctx.CurrentFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreNotEqual(0, ctx.CurrentFrame.Context.BP);

                    //It's just a buffer, so doesn't have a value yet
                    Assert.AreEqual(1, locals.Length);

                    //We should still be able to get the variables of the previous frame
                    var previousLocals = ctx.PreviousFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreEqual(3, previousLocals.Length);

                    Assert.IsTrue(previousLocals[0].Value.IsEquivalentTo(3));
                    Assert.IsTrue(previousLocals[1].Value.IsEquivalentTo(ctx.Process.CommandLine[0]));
                    Assert.IsTrue(previousLocals[2].Value.IsEquivalentTo(NativeTestType.Com));
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Variables_Native_InMiddleOfFrame_FirstFrame()
        {
            //Once the frame has been fully established, we should still be able to see our locals properly

            TestNativeMain(
                ctx =>
                {
                    Log.Debug<CordbEngineVariableTests>("Moving to BindLifetimeToParentProcess");
                    ctx.MoveToCall("BindLifetimeToParentProcess");

                    var locals = ctx.PreviousFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreEqual(3, locals.Length);

                    Assert.AreEqual(3, locals[0].Value);
                    Assert.AreEqual(ctx.Process.CommandLine[0], locals[1].Value);
                    Assert.AreEqual(NativeTestType.Com, locals[2].Value);
                }
            );
        }
    }
}

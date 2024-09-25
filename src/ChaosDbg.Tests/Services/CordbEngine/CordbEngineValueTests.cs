using System.Linq;
using ChaosDbg.Cordb;
using ChaosLib;
using ChaosLib.Symbols.MicrosoftPdb.TypedData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestApp;

namespace ChaosDbg.Tests
{
    [MTATestClass]
    public class CordbEngineValueTests : BaseTest
    {
        #region Native

        [TestMethod]
        public void CordbEngine_Value_Native_Variables_FirstFrame_Unestablished()
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

                    foreach (var local in locals)
                        Assert.IsTrue(IsBad(local), $"Local {local} was not bad");
                }
            );
        }

        [TestMethod]
        public void CordbEngine_Value_Native_Variables_SecondFrame_Unestablished()
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
        public void CordbEngine_Value_Native_Variables_FirstFrame_Established()
        {
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
        public void CordbEngine_Value_Native_Variables_SecondFrame_Established()
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
        public void CordbEngine_Value_Native_Variables_InMiddleOfFrame_FirstFrame()
        {
            //Once the frame has been fully established, we should still be able to see our locals properly

            TestNativeMain(
                ctx =>
                {
                    Log.Debug<CordbEngineValueTests>("Moving to BindLifetimeToParentProcess");
                    ctx.MoveToCall("BindLifetimeToParentProcess");

                    var locals = ctx.CurrentFrame.Variables.Cast<CordbNativeVariable>().ToArray();

                    Assert.AreEqual(3, locals.Length);

                    Assert.IsTrue(locals[0].Value.IsEquivalentTo(3));
                    Assert.IsTrue(locals[1].Value.IsEquivalentTo(ctx.Process.CommandLine[0]));
                    Assert.IsTrue(locals[2].Value.IsEquivalentTo(NativeTestType.Com));
                }
            );
        }

        private bool IsBad(CordbNativeVariable variable)
        {
            static bool IsBadInternal(ITypedValue value)
            {
                if (value is BadMemoryValue)
                    return true;

                if (value is PrimitiveTypedValue p)
                    return p.Value is BadMemoryValue;

                if (value is PointerTypedValue o)
                    return IsBadInternal(o.Value);

                return false;
            }

            return IsBadInternal(variable.Value);
        }

        #endregion
    }
}

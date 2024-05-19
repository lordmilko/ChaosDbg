using System;
using System.Linq;
using ChaosDbg.PowerShell.Host;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    [TestClass]
    public class ChaosDbgHostTests : BaseTest
    {
        [TestMethod]
        public void ChaosDbgHost_NativeRead_Enter()
        {
            //type ls and hit enter

            TestShell(() => "ls" + Environment.NewLine, v => Assert.IsTrue(v.Any(s => s.Contains("LastWriteTime"))));
        }

        [TestMethod]
        public void ChaosDbgHost_NativeRead_CompleteFirst()
        {
            //do get-p and hit tab

            TestShell(() => "get-p\t", v => Assert.AreEqual("Get-Package", v.Single()));
        }

        [TestMethod]
        public void ChaosDbgHost_NativeRead_CompleteSecond()
        {
            //do get-p and hit tab twice

            TestShell(
                new Func<string>[]
                {
                    () => "get-p\t",
                    () => "Get-Package\t" //The previous completion was written to the buffer, and we then hit tab on it
                },
                v =>
                {
                    Assert.AreEqual(2, v.Length);
                    Assert.AreEqual("Get-Package", v[0]);
                    Assert.AreEqual("Get-PackageProvider", v[1]);
                }
            );
        }

        [TestMethod]
        public void ChaosDbgHost_NativeRead_CtrlC_EmptyLine()
        {
            //brings up another newline

            TestShell(() => null, v =>
            {
                Assert.AreEqual(2, v.Length);
                Assert.AreEqual(Environment.NewLine, v[0]);
                Assert.IsTrue(v[1].StartsWith("PS "));
            });
        }

        [TestMethod]
        public void ChaosDbgHost_NativeRead_CtrlC_LineHasContent()
        {
            //brings up another newline

            TestShell(
                () => string.Empty, //If you typed "foo" and hit Ctrl+C it would return an empty string. If you just typed "foo" with no newline, it would just begin doing ReadConsole again,
                v =>
                {
                    Assert.AreEqual(2, v.Length);
                    Assert.AreEqual(Environment.NewLine, v[0]);
                    Assert.IsTrue(v[1].StartsWith("PS "));
                }
            );
        }

        private void TestShell(Func<string> action, Action<string[]> validate) =>
            TestShell(new[] {action}, validate);

        private void TestShell(
            Func<string>[] actions,
            Action<string[]> validate)
        {
            MockTerminal terminal = null;

            var actionIndex = 0;

            terminal = new MockTerminal
            {
                OnReadConsole = () =>
                {
                    if (actionIndex < actions.Length)
                    {
                        var result = actions[actionIndex]();
                        actionIndex++;
                        return result;
                    }

                    return "exit" + Environment.NewLine;
                }
            };

            ChaosShell.Start(terminal, "-noprofile", "-noninteractive");

            //Skip over the prompt at the start. The prompt at the end seems to not always be guaranteed based on our test (e.g. if we're doing a completion)
            var output = terminal.Output.Skip(1).ToArray();

            validate(output);
        }
    }
}

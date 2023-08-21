using System;
using System.IO;
using ChaosDbg.MSBuild;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests
{
    //We can't use source generator version 4+ in VS2019, and whats worse VS2019 seems to freak out when you source generate any files and stop syntax highlighting your attributes/base classes
    //in the project that the files were generated for for some reason. As such, we'll work around this by writing our own source generator that does the same thing!
    [TestClass]
    public class CustomSourceGeneratorTests
    {
        [TestMethod]
        public void CommandSourceGenerator_Success()
        {
            var input = @"
public partial class foo
{
    [ChaosDbg.Reactive.RelayCommandAttribute]
    public void OnFoo()
    {
    }
}";

            var expected = @"
namespace ChaosDbg.ViewModel
{
    public partial class foo
    {
        /// <summary>
        /// The backing field for <see cref=""FooCommand""/>
        /// </summary>
        private IRelayCommand fooCommand;

        /// <summary>
        /// Gets an <see cref=""IRelayCommand""/> instance wrapping <see cref=""OnFoo""/>
        /// </summary>
        public IRelayCommand FooCommand
        {
            get
            {
                if (fooCommand == null)
                    fooCommand = new RelayCommand(OnFoo);

                return fooCommand;
            }
        }
    }
}";
            TestResult(input, expected);
        }

        [TestMethod]
        public void CommandSourceGenerator_MissingPartial()
        {
            var input = @"
public class foo
{
    [RelayCommand]
    public void OnFoo()
    {
    }
}";
            AssertEx.Throws<InvalidOperationException>(
                () => TestResult(input, string.Empty),
                "Type 'foo' must be marked as partial to generate RelayCommand for method 'OnFoo'"
            );
        }

        [TestMethod]
        public void CommandSourceGenerator_IgnoresOtherAttributes()
        {
            var input = @"
public partial class foo
{
    [Obsolete]
    public void OnFoo()
    {
    }
}";
            TestResult(input, string.Empty);
        }

        [TestMethod]
        public void CommandSourceGenerator_RemoteException()
        {
            var input = @"
public partial class foo
{
    [RelayCommand]
    public void OnFoo()
    {
    }
}";
            AssertEx.Throws<ArgumentNullException>(
                () => TestResult(input, null),
                "Value cannot be null"
            );
        }

        [TestMethod]
        public void CommandSourceGenerator_CanExecute()
        {
            var input = @"
public partial class foo
{
    [RelayCommand(CanExecute = nameof(foo))]
    public void OnFoo()
    {
    }
}";
            var expected = @"
namespace ChaosDbg.ViewModel
{
    public partial class foo
    {
        /// <summary>
        /// The backing field for <see cref=""FooCommand""/>
        /// </summary>
        private IRelayCommand fooCommand;

        /// <summary>
        /// Gets an <see cref=""IRelayCommand""/> instance wrapping <see cref=""OnFoo""/>
        /// </summary>
        public IRelayCommand FooCommand
        {
            get
            {
                if (fooCommand == null)
                    fooCommand = new RelayCommand(OnFoo, foo);

                return fooCommand;
            }
        }
    }
}";

            TestResult(input, expected);
        }

        private void TestResult(string input, string expected)
        {
            input = "namespace Test {" + Environment.NewLine + input + Environment.NewLine + "}";
            expected = expected?.Trim();

            string CreateCSharpFile()
            {
                var original = Path.GetTempFileName();
                var cs = Path.ChangeExtension(original, ".cs");
                File.Move(original, cs);
                return cs;
            }

            var tmpInputFile = CreateCSharpFile();

            string tmpOutputFile = null;

            if (expected != null)
            {
                tmpOutputFile = CreateCSharpFile();
            }            

            File.WriteAllText(tmpInputFile, input);

            try
            {
                var task = new GenerateViewModels
                {
                    Files = new[] { tmpInputFile },
                    Output = tmpOutputFile,

                    BuildEngine = new MockBuildEngine()
                };

                task.Execute();

                if (expected != null)
                {
                    var actual = File.ReadAllText(tmpOutputFile).Trim();

                    Assert.AreEqual(expected, actual);
                }
            }
            finally
            {
                if (File.Exists(tmpInputFile))
                    File.Delete(tmpInputFile);

                if (tmpOutputFile != null && File.Exists(tmpOutputFile))
                    File.Delete(tmpOutputFile);
            }
        }
    }
}

using System.IO;
using ChaosDbg.Evaluator.Cpp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ChaosDbg.Tests.Evaluator
{
    [TestClass]
    public class CppEvaluatorTests
    {
#if DEBUG
        private void StressTest(string directory)
        {
            var files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);

                switch (ext)
                {
                    case ".c":
                    case ".cpp":
                    case ".h":
                    case ".hpp":
                        break;

                    case ".txt":
                    case ".ini":
                    case ".cmd":
                        continue;

                    default:
                        continue;
                }

                var str = File.ReadAllText(file);

                var lexer = new CppLexer(str);
                lexer.LexInternal();
            }
        }
#endif
    }
}

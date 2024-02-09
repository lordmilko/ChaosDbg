using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ChaosDbg.Tests
{
    static class IDAProvider
    {
        private static string chaosDbgTemp = Path.Combine(Path.GetTempPath(), "ChaosDbgTemp");

        public static string GetLst(string assembly)
        {
            var name = Path.GetFileNameWithoutExtension(assembly);

            var lstPath = Path.Combine(chaosDbgTemp, $"{name}.lst");

            if (File.Exists(lstPath))
                return lstPath;

            return GenerateLst(assembly);
        }

        private static string GenerateLst(string assembly)
        {
            if (!File.Exists(assembly))
                throw new FileNotFoundException($"Cannot find assembly file '{assembly}'");

            var ida = LocateIDAPro();

            if (!Directory.Exists(chaosDbgTemp))
                Directory.CreateDirectory(chaosDbgTemp);

            var name = Path.GetFileNameWithoutExtension(assembly);

            var lstPath = Path.Combine(chaosDbgTemp, $"{name}.lst");

            if (File.Exists(lstPath))
                File.Delete(lstPath);

            //IDA can't deal with paths containing spaces passed to -S. Wrapping the path in quotes or various escape
            //sequences doesn't seem to work
            var scriptPath = Path.Combine("C:\\", $"ChaosDbgTemp_{name}.py");

            var idaTemp = Path.Combine(chaosDbgTemp, name);

            if (!Directory.Exists(idaTemp))
                Directory.CreateDirectory(idaTemp);

            var script = $@"
import idc
auto_wait()
idc.gen_file(idc.OFILE_LST , ""{lstPath.Replace("\\", "\\\\")}"", 0, idc.BADADDR, 0)
import ida_pro
ida_pro.qexit(0)
";
            File.WriteAllText(scriptPath, script);

            try
            {
                //Remove -A to have a UI and see any error messages
                var process = Process.Start(ida, $"-A -S\"{scriptPath}\" -c -o\"{idaTemp}\" \"{assembly}\"");
                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"IDA Pro exited with code {process.ExitCode}");

                if (!File.Exists(lstPath))
                    throw new InvalidOperationException($"IDA Pro exited successfully however '{lstPath}' could not be found");

                Directory.Delete(idaTemp, true);

                return lstPath;
            }
            finally
            {
                File.Delete(scriptPath);
            }
        }

        private static string LocateIDAPro()
        {
            if (IntPtr.Size == 8)
            {
                var programFilesx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");

                var idaCandidates = Directory.EnumerateDirectories(programFilesx86, "IDA Pro*").ToArray();

                if (idaCandidates.Length == 0)
                    throw new InvalidOperationException("Could not locate IDA Pro");

                var idaPath = Path.Combine(idaCandidates[0], "ida64.exe");

                if (!File.Exists(idaPath))
                    throw new InvalidOperationException($"Could not find ida64.exe under {idaCandidates[0]}");

                return idaPath;
            }
            else
                throw new NotImplementedException("Locating IDA Pro from a 32-bit test host is not implemented");
        }
    }
}

using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using ChaosDbg.IL;

[assembly: InternalsVisibleTo("chaos")]
[assembly: InternalsVisibleTo("ChaosDbg")]
[assembly: InternalsVisibleTo("ChaosDbg.GeneratedCode")]
[assembly: InternalsVisibleTo("ChaosLib.GeneratedCode")]
[assembly: InternalsVisibleTo("ChaosDbg.Tests")]

[assembly: DebuggerTypeProxy(typeof(ILGeneratorDebugView), Target = typeof(ILGenerator))]

//init only properties require this type be defined, which is not present in .NET Standard / .NET Framework
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

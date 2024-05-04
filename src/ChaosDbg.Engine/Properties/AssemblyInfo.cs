using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("chaos")]
[assembly: InternalsVisibleTo("ChaosDbg")]
[assembly: InternalsVisibleTo("ChaosDbg.GeneratedCode")]
[assembly: InternalsVisibleTo("ChaosLib.GeneratedCode")]
[assembly: InternalsVisibleTo("ChaosDbg.Tests")]

//init only properties require this type be defined, which is not present in .NET Standard / .NET Framework
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

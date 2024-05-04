using System.Runtime.CompilerServices;
using Unit = Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Unit.Parallelize(Scope = Unit.ExecutionScope.MethodLevel)]

[assembly: InternalsVisibleTo("ChaosDbg.GeneratedCode")]
[assembly: InternalsVisibleTo("ChaosLib.GeneratedCode")]

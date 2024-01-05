using System.Runtime.CompilerServices;
using Unit = Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: Unit.Parallelize(Scope = Unit.ExecutionScope.ClassLevel)]

[assembly: InternalsVisibleTo("ChaosDbg.GeneratedCode")]

using System.Management.Automation;
using ChaosDbg.Cordb;

namespace ChaosDbg.PowerShell.Cmdlets.Symbol
{
    [Cmdlet(VerbsCommon.Get, "DbgSymbol")]
    public class GetDbgSymbol : ChaosCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var symbols = ((CordbEngine) ActiveEngine).Process.Symbols;

            var results = symbols.EnumerateSymbols(Name);

            foreach (var result in results)
                WriteObject(result);
        }
    }
}

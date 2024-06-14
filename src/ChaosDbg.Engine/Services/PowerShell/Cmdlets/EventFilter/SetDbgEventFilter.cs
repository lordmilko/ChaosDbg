using System;
using System.Linq;
using System.Management.Automation;

namespace ChaosDbg.PowerShell.Cmdlets.EventFilter
{
    //[Cmdlet(VerbsCommon.Set, "DbgEventFilter")]
    public class SetDbgEventFilter : ChaosCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var filter = GetSingleFilter();

            throw new NotImplementedException();
        }

        private IDbgEventFilter GetSingleFilter()
        {
            var filter = ActiveEngine.Session.EventFilters.FirstOrDefault(e => Name.Equals(e.Alias, StringComparison.OrdinalIgnoreCase));

            if (filter == null)
                throw new NotImplementedException($"Could not find a filter with name {Name}");

            return filter;
        }
    }
}

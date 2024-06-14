using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace ChaosDbg.PowerShell.Cmdlets.EventFilter
{
    [Cmdlet(VerbsCommon.Get, "DbgEventFilter")]
    public class GetDbgEventFilter : ChaosCmdlet
    {
        [Parameter(Mandatory = false, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            IEnumerable<IDbgEventFilter> filters = ActiveEngine.Session.EventFilters;

            if (Name != null)
            {
                var wildcard = new WildcardPattern(Name, WildcardOptions.IgnoreCase);

                filters = filters.Where(f => wildcard.IsMatch(f.Name) || wildcard.IsMatch(f.Alias));
            }

            foreach (var filter in filters)
                WriteObject(filter);
        }
    }
}

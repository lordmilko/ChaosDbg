using System.Management.Automation;
using ChaosDbg.DbgEng.Model;

namespace ChaosDbg.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "TtdFunctionCall")]
    public class GetTtdFunctionCall : DbgEngCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string[] Name { get; set; }

        [Parameter(Mandatory = false)]
        public SwitchParameter Model { get; set; }

        protected override void ProcessRecord()
        {
            if (Model)
            {
                FromDbgModel();
            }
            else
            {
                FromCursor();
            }
        }

        private void FromDbgModel()
        {
            //I'm not sure whether I've noticed that in some versions of TTD, you can do wildcards to dx @$cursession.TTD.Calls
            //while in other's you can't. It's possible the exception that is getting thrown when TTD tries to resolve the expression
            //specified to GET_EXPRESSION_EX gets ignored and it'll resolve the pattern some other way; can't remember

            var session = ActiveEngine.GetModelSession();

            var ttd = session.TTD;

            foreach (var result in ttd.Calls(Name))
            {
                var call = new TtdModelFunctionCall(result);

                WriteObject(call);
            }
        }

        private void FromCursor()
        {
            //This is based on what @$cursession.TTD.Calls() does

            var engine = ActiveEngine;

            var results = engine.TTD.GetFunctionCalls(Name);

            foreach (var result in results)
                WriteObject(result);
        }
    }
}

using System;
using System.Management.Automation;

namespace ChaosDbg.PowerShell
{
    struct ProgressHolder : IDisposable
    {
        private PSCmdlet cmdlet;

        public ProgressRecord ProgressRecord { get; }

        public ProgressHolder(PSCmdlet cmdlet, string activity, string statusDescription)
        {
            this.cmdlet = cmdlet;

            ProgressRecord = new ProgressRecord(1, activity, statusDescription);
        }

        public void Dispose()
        {
            ProgressRecord.RecordType = ProgressRecordType.Completed;
            cmdlet.WriteProgress(ProgressRecord);
        }
    }
}

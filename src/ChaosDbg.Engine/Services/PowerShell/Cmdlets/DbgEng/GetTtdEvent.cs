using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using ChaosDbg.DbgEng.Model;
using ChaosDbg.TTD;
using ClrDebug;
using ClrDebug.TTD;

namespace ChaosDbg.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "TtdEvent")]
    public class GetTtdEvent : DbgEngCmdlet
    {
        [Parameter(Mandatory = false)]
        public SwitchParameter Model { get; set; }

        protected override void ProcessRecord()
        {
            if (Model)
                FromDbgModel();
            else
                FromCursor();
        }

        private void FromDbgModel()
        {
            var process = ActiveEngine.GetModelProcess();

            var ttd = process.TTD;

            var debugOutput = ((IEnumerable<dynamic>) ttd.DebugOutput).ToDictionary(v => new TtdModelPosition(v.Position));
            var events = ((IEnumerable<dynamic>) ttd.Events).ToArray();

            var results = new List<TtdModelEvent>();

            foreach (var @event in events)
            {
                var type = (TtdModelEventType) Enum.Parse(typeof(TtdModelEventType), (string) @event.Type);

                switch (type)
                {
                    case TtdModelEventType.ModuleLoaded:
                        results.Add(new TtdModelModuleLoadedEvent(@event));
                        break;

                    case TtdModelEventType.ModuleUnloaded:
                        results.Add(new TtdModelModuleUnloadedEvent(@event));
                        break;

                    case TtdModelEventType.ThreadCreated:
                        results.Add(new TtdModelThreadCreatedEvent(@event));
                        break;

                    case TtdModelEventType.ThreadTerminated:
                        results.Add(new TtdModelThreadTerminatedEvent(@event));
                        break;

                    case TtdModelEventType.Exception:
                        //You can retrieve the string that was printed directly from an ExceptionEvent by inspecting its ExceptionInformation values. These unfortunately are not exposed via the DbgEng Data Model however!
                        if (@event.Exception.Type == "DebugPrint")
                        {
                            if (debugOutput.TryGetValue(new TtdModelPosition(@event.Position), out var match))
                            {
                                results.Add(new TtdModelDebugPrintEvent(@event, match));
                                continue;
                            }
                        }

                        results.Add(new TtdModelExceptionEvent(@event));
                        break;

                    default:
                        throw new UnknownEnumValueException(type);
                }
            }

            foreach (var result in results)
                WriteObject(result);
        }

        private void FromCursor()
        {
            var replayEngine = ActiveEngine.TTD.ReplayEngine;

            Lazy<Cursor> cursor = new Lazy<Cursor>(() => replayEngine.NewCursor());

            var results = new List<TtdRawEvent>();

            //Threads

            foreach (var @event in replayEngine.ThreadCreatedEventList)
                results.Add(new TtdRawThreadCreatedEvent(@event));

            foreach (var @event in replayEngine.ThreadTerminatedEventList)
                results.Add(new TtdRawThreadTerminatedEvent(@event));

            //Modules

            foreach (var @event in replayEngine.ModuleLoadedEventList)
                results.Add(new TtdRawModuleLoadedEvent(@event));

            foreach (var @event in replayEngine.ModuleUnloadedEventList)
                results.Add(new TtdRawModuleUnloadedEvent(@event));

            //Exceptions

            foreach (var @event in replayEngine.ExceptionEventList)
            {
                switch (@event.exception.ExceptionCode)
                {
                    case NTSTATUS.DBG_PRINTEXCEPTION_C:
                    case NTSTATUS.DBG_PRINTEXCEPTION_WIDE_C:
                        results.Add(new TtdRawDebugPrintEvent(@event, cursor.Value));
                        break;

                    default:
                        results.Add(new TtdRawExceptionEvent(@event));
                        break;
                }
            }

            results.Sort((a, b) => a.Position.CompareTo(b.Position));

            foreach (var result in results)
                WriteObject(result);
        }
    }
}

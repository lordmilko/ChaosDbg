using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Evaluator.Masm;
using ChaosDbg.Evaluator.Masm.Syntax;
using ChaosDbg.TTD;
using ChaosLib;
using ChaosLib.Memory;
using ChaosLib.TTD;
using ClrDebug.TTD;
using Iced.Intel;
using SymHelp.Symbols;

namespace ChaosDbg.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "TtdDataFlow")]
    public class GetTtdDataFlow : DbgEngCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Expression { get; set; }

        [Parameter(Mandatory = false, Position = 1)]
        public int Size { get; set; }

        [Parameter(Mandatory = false)]
        public Position Position { get; set; }

        [Alias("IncludePtrOrigin")]
        [Parameter(Mandatory = false)]
        public SwitchParameter IncludePointerOrigin { get; set; }

        protected override unsafe void ProcessRecord()
        {
            var cursor = ActiveEngine.TTD.ReplayEngine.NewCursor();

            if (HasParameter(nameof(Position)))
                cursor.SetPosition(Position);
            else
            {
                var engineCursor = ActiveEngine.TTD.UnsafeEngineCursor;

                var enginePosition = engineCursor.GetPosition();

                cursor.SetPosition(enginePosition);
            }

            //Create an isolated deferred symbol manager based on the symbols loaded at the current position
            using var symbolManager = new TtdSymbolManager(GetService<INativeLibraryProvider>(), GetService<ISymSrv>(), cursor);
            symbolManager.Update();

            var registerContext = cursor.GetCrossPlatformContext();

            var stream = new MemoryReaderStream(new TtdCursorMemoryReader(cursor));
            var disassembler = NativeDisassembler.FromStream(stream, ActiveEngine.ActiveProcess.Is32Bit, new DbgEngDisasmSymbolResolver(ActiveEngine.ActiveClient));

            var evaluationContext = new TtdMasmEvaluatorContext(cursor, registerContext);

            //Expression may either identify a register whose value we want to find the origin of, or identify a memory location
            //that we want to analyze.

            var (tokens, lexErrors) = MasmLexer.Lex(Expression);

            if (lexErrors.Length > 0)
                throw new InvalidExpressionException(string.Format(lexErrors[0], Expression));

            disassembler.BaseStream.Position = registerContext.IP;
            var instr = disassembler.Disassemble();

            var startName = symbolManager.GetSymbol(registerContext);

            TtdDataFlowJob initial;
            long target;

            var thread = cursor.GetThreadInfo();
            var position = cursor.GetPosition();

            //The last token will always be an EndOfFile token
            if (tokens.Length == 2 && tokens[0].Kind == MasmSyntaxKind.RegisterLiteralToken)
            {
                //The expression was literally just a register (e.g. "rdx"). Read the value of that register, and trace back through register + memory operations
                //to determine where the value came from

                var register = (Register) tokens[0].Value;

                target = registerContext.GetRegisterValue(register);

                initial = new TtdDataFlowEntryRegisterJob(register, registerContext, new TtdDataFlowItem(target, startName, thread, position, instr)
                {
                    Location = "Search Start"
                });
            }
            else
            {
                //It's a complex expression that identifies a memory location. Evaluate the memory location.
                //We need to remove the outer later of poi if there is one in order to get the pointer value we're using

                var (syntax, parseErrors) = MasmParser.Parse(tokens);

                if (parseErrors.Length > 0)
                    throw new InvalidExpressionException(string.Format(parseErrors[0], Expression));

                while (syntax is ParenthesizedExpressionSyntax pa)
                    syntax = pa.Expression;

                /* The user can specify several potential types of expressions:
                 *
                 * rdx -> trace where the value in rdx came from
                 * rdx+1 -> trace where the value pointed to by rdx+1 came from
                 * poi(rdx) -> trace where the value pointed to by rdx came from
                 * poi(rdx+1) -> trace where the value pointed to by rdx+1 came from
                 *
                 * Thus, we can see that specifying poi is merely used as a "hint" to us to treat the expression as a pointer, however we don't literally need to do the poi indirection
                 * as part of the expression evaluation (we'll do it manually afterwards regardless). Thus, strip off the outer poi if there is one */
                if (syntax is PseudoFunctionExpressionSyntax {Token: {Kind: MasmSyntaxKind.PoiKeyword or MasmSyntaxKind.PhysicalPoiKeyword}} p)
                    syntax = p.Operand;

                var visitor = new MasmEvaluatorVisitor(evaluationContext);
                var pointerAddress = syntax.Accept(visitor);

                //And pointed to by that memory location is the value we're interested in tracing
                target = (long) (void*) cursor.QueryMemoryBuffer<IntPtr>(pointerAddress, QueryMemoryPolicy.Default);

                initial = new TtdDataFlowEntryPointerJob(pointerAddress, new TtdDataFlowItem(target, startName, thread, position, null));
            }

            var ctx = new TtdDataFlowContext(cursor, symbolManager, disassembler, target, Size);

            var results = new HashSet<TtdDataFlowItem>();
            var jobQueue = new Queue<TtdDataFlowJob>();
                        
            results.Add(initial.ParentEvent);
            jobQueue.Enqueue(initial);

            using var progressHolder = new ProgressHolder(this, "TTD Data Flow", "Empty");

            /* There's four kinds of operations we need to trace
             * 1. Register to Register: mov rax, rbx  -> manually step back
             * 2a. Memory to Register: mov rax, [rbx] -> manually step back
             * 2b.                                    -> where did the rbx come from?
             * 3. Register to Memory: mov [rbx], rax  -> watchpoint
             *
             * We don't care about tracing where rbx came from in 3. The value didn't come "from" there, it went "to" there.
             * Therefore, it's only scenario 2 where we might emit two paths we need to follow. We follow the "most recent" event
             * to now first, so that we don't run too far away and then have to move the cursor back a big distance
             */
            while (jobQueue.Count > 0)
            {
                var parentJob = jobQueue.Dequeue();

                progressHolder.ProgressRecord.StatusDescription = $"Processing record {parentJob} (Found: {results.Count}, Remaining: {jobQueue.Count})";
                WriteProgress(progressHolder.ProgressRecord);

                try
                {
                    foreach (var childJob in parentJob.EnumerateDataEvents(ctx, CancellationToken))
                    {
                        if (CancellationToken.IsCancellationRequested)
                            return;

                        if (!IncludePointerOrigin && (childJob is TtdDataFlowOp1PtrJob || childJob.ParentEvent.Tag == TtdDataFlowTag.PointerOrigin))
                            continue;

                        jobQueue.Enqueue(childJob);
                        results.Add(childJob.ParentEvent);
                    }
                }
                catch (TtdEndOfTraceException)
                {
                }
            }

            var sorted = results.OrderBy(v => v.Position).ToArray();

            foreach (var item in sorted)
                WriteObject(item);
        }
    }
}

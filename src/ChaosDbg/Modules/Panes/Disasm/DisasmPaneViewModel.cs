using System;
using System.IO;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Reactive;
using ChaosDbg.Text;
using ChaosDbg.Theme;
using ChaosLib.Memory;
using ClrDebug.DbgEng;
using ChaosLib.PortableExecutable;

namespace ChaosDbg.ViewModel
{
    public class DisasmPaneViewModel : ViewModelBase
    {
        [Reactive]
        public virtual ITextBuffer Buffer { get; set; }

        private Font font;

        private DebugEngineProvider engineProvider;
        private IDbgEngine engine => engineProvider.ActiveEngine;

        public event EventHandler<AddressChangedEventArgs> AddressChanged;

        public DisasmPaneViewModel(IThemeProvider themeProvider, DebugEngineProvider engineProvider)
        {
            font = themeProvider.GetTheme().ContentFont;
            this.engineProvider = engineProvider;

            engineProvider.EngineStatusChanged += Engine_EngineStatusChanged;
        }

        private void Engine_EngineStatusChanged(object sender, EngineStatusChangedEventArgs e)
        {
            if (e.NewStatus == EngineStatus.Break)
            {
                //We've broken into the debugger! Find out what module we're in, create a buffer for displaying disassembly
                //and notify the UI that the active address of our buffer has changed
                var addr = ((DbgEngEngine) engine).ActiveClient.Control.Evaluate("@$scopeip", DEBUG_VALUE_TYPE.INT64).Value.I64;

                var module = ((DbgEngEngine) engine).ActiveProcess.Modules.GetModuleForAddress(addr);
                var navigator = GetNavigator(module.BaseAddress);

                Buffer = new DbgEngDisasmTextBuffer(
                    font,
                    module,
                    navigator
                );

                var rva = addr - module.BaseAddress;

                AddressChanged?.Invoke(this, new AddressChangedEventArgs(rva));
            }
        }

        private CodeNavigator GetNavigator(long baseAddress)
        {
            var dbgEngStream = new DbgEngMemoryStream(((DbgEngEngine) engine).Session.ActiveClient);
            var relativeStream = new RelativeToAbsoluteStream(dbgEngStream, baseAddress);
            relativeStream.Seek(0, SeekOrigin.Begin);

            var peFile = PEFile.FromStream(relativeStream, true);

            var disasmEngine = NativeDisassembler.FromStream(dbgEngStream, engine.ActiveProcess.Is32Bit);

            var nav = new CodeNavigator(baseAddress, peFile, disasmEngine);

            return nav;
        }
    }
}

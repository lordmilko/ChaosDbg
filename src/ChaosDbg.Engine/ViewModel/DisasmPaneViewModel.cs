using System;
using System.IO;
using ChaosDbg.DbgEng;
using ChaosDbg.Disasm;
using ChaosDbg.Reactive;
using ChaosDbg.Text;
using ChaosDbg.Theme;
using ChaosLib.Memory;
using ClrDebug.DbgEng;
using ChaosLib.Metadata;

namespace ChaosDbg.ViewModel
{
    public class DisasmPaneViewModel : ViewModelBase
    {
        [Reactive]
        public virtual ITextBuffer Buffer { get; set; }

        private Font font;

        private DbgEngEngine engine;
        private INativeDisassemblerProvider nativeDisassemblerProvider;
        private IPEFileProvider peFileProvider;

        public event EventHandler<AddressChangedEventArgs> AddressChanged;

        public DisasmPaneViewModel(IThemeProvider themeProvider, DbgEngEngine engine, INativeDisassemblerProvider nativeDisassemblerProvider, IPEFileProvider peFileProvider)
        {
            font = themeProvider.GetTheme().ContentFont;
            this.engine = engine;
            this.nativeDisassemblerProvider = nativeDisassemblerProvider;
            this.peFileProvider = peFileProvider;

            engine.EngineStatusChanged += Engine_EngineStatusChanged;
        }

        private void Engine_EngineStatusChanged(object sender, EngineStatusChangedEventArgs e)
        {
            if (e.NewStatus == DEBUG_STATUS.BREAK)
            {
                //We've broken into the debugger! Find out what module we're in, create a buffer for displaying disassembly
                //and notify the UI that the active address of our buffer has changed
                var addr = engine.ActiveClient.Control.Evaluate("@$scopeip", DEBUG_VALUE_TYPE.INT64).Value.I64;

                var module = engine.Modules.GetModuleForAddress(addr);
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
            var dbgEngStream = new DbgEngMemoryStream(engine.Session.UiClient);
            var relativeStream = new RelativeToAbsoluteStream(dbgEngStream, baseAddress);
            relativeStream.Seek(0, SeekOrigin.Begin);

            var peFile = peFileProvider.ReadStream(relativeStream, true);

            var disasmEngine = nativeDisassemblerProvider.CreateDisassembler(dbgEngStream, engine.Target.Is32Bit);

            var nav = new CodeNavigator(peFile, disasmEngine);

            return nav;
        }
    }
}

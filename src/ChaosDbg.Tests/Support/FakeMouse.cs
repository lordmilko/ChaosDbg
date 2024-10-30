using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using ChaosLib;

namespace ChaosDbg.Tests
{
    internal class FakeMouse
    {
        private const int Activate = 2;
        private const int AbsoluteMove = 16;
        private const int Button1Press = 64;
        private const int Button1Release = 128;

        #region Reflection

        private static readonly Type rawMouseInputReportType = typeof(InputManager).Assembly.GetType("System.Windows.Input.RawMouseInputReport");
        private static readonly ConstructorInfo rawMouseInputReportTypeCtor = rawMouseInputReportType.GetConstructors().Single();
        private static readonly FieldInfo isSynchronizeFieldInfo = rawMouseInputReportType.GetFieldInfo("_isSynchronize");

        private static readonly Type inputReportEventArgsType = typeof(InputManager).Assembly.GetType("System.Windows.Input.InputReportEventArgs");
        private static readonly ConstructorInfo inputReportEventArgsCtor = inputReportEventArgsType.GetConstructors().Single();

        private static readonly Type rawMouseActionsType = typeof(InputManager).Assembly.GetType("System.Windows.Input.RawMouseActions");

        private static readonly RoutedEvent previewInputReportEvent = (RoutedEvent) typeof(InputManager).GetFieldInfo("PreviewInputReportEvent").GetValue(null);

        private static readonly MethodInfo changeMouseOver = typeof(MouseDevice).GetMethodInfo("ChangeMouseOver");
        private static readonly MethodInfo localHitTest = typeof(MouseDevice).GetMethod("LocalHitTest", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(bool), typeof(Point), typeof(PresentationSource), typeof(IInputElement).MakeByRefType(), typeof(IInputElement).MakeByRefType() }, null);

        #endregion

        //Scope these properties to a given DispatcherThread
        [ThreadStatic]
        private static bool isFaking;

        [ThreadStatic]
        private static bool isButtonDown;

        private Visual window;
        private bool isActive;

        //AppDomainTestMethodAttribute sets this, forcing the cctor to run in the original appdomain
        public static bool InstallHook;

        static FakeMouse()
        {
            //I think when we set InstallHook, the cctor might run first, so we can't rely on properties to protect us from running again in a remote domain
            if (AppDomain.CurrentDomain.FriendlyName.Contains("ChaosDbg TestDomain"))
                return;

            ChaosLib.Detour.DetourBuilder.AddPInvokeHook(new[] { "GetKeyState","WindowFromPoint" }, typeof(User32.Native), ctx =>
            {
                //The callback runs in the original AppDomain, but I don't think that any of these methods used by the hook actually need to access the current AppDomain; InputManager is used by methods that are called manually

                if (isFaking)
                {
                    switch (ctx.Name)
                    {
                        case "GetKeyState":
                            if (ctx.Arg<VirtualKey>("nVirtKey") == VirtualKey.LButton)
                            {
                                //I think WPF checks against 32768 because it uses a short, so in short.MinValue the high bit is also set
                                if (isButtonDown)
                                    return ushort.MaxValue;
                            }

                            break;

                        case "WindowFromPoint":
                            return HiddenWindowFromPoint(ctx.Arg<POINT>("Point"));
                    }
                }

                return ctx.InvokeOriginal();
            });
        }

        public FakeMouse(Visual window)
        {
            this.window = window;
        }

        public void Click(int clientX, int clientY)
        {
            Down(clientX, clientY);
            Up(clientX, clientY);
        }

        public void Down(int clientX, int clientY)
        {
            //Fake having moved over the specified point and clicking down on it. We can't rely on WPF to do this for us
            //as WPF checks for the global top level window, but we want to allow our window to be obstructed by other windows
            //for the purposes of unit testing
            Move(clientX, clientY);

            //Certain elements (such as TextBox) may query what the current state of the mouse is,
            //and abort out if the state isn't pressed. As such, fake the state being "Pressed" until
            //we call Up()
            isButtonDown = true;

            SendPreviewInputReport(Button1Press, clientX, clientY);
        }

        public void Up(int clientX, int clientY)
        {
            isButtonDown = false;

            SendPreviewInputReport(Button1Release, clientX, clientY);
        }

        public void Move(int clientX, int clientY)
        {
            SendPreviewInputReport(AbsoluteMove, clientX, clientY);
        }

        private void SendPreviewInputReport(int actionFlags, int x, int y)
        {
            //From HwndMouseInputProvider.FilterMessage and HwndMouseInputProvider.ReportInput

            if (!isActive)
            {
                //MouseDevice._inputSource will be null until an Activate message has been received. The Activate message normally gets sent by the HwndMouseInputProvider when it sees that its _active field is false
                actionFlags |= Activate;
                isActive = true;
            }

            var hwnd = PresentationSource.FromVisual(window);

            if (hwnd == null)
                throw new NotImplementedException();

            var actions = Enum.ToObject(rawMouseActionsType, actionFlags);

            //The timestamp FilterMessage uses comes from user32!GetMessageTime, however I've seen that WPF can also send fake
            //messages using Environment.TickCount
            var report = rawMouseInputReportTypeCtor.Invoke(new object[]
            {
                InputMode.Foreground,  //mode,
                Environment.TickCount, //timestamp,
                hwnd,                  //inputSource,
                actions,               //actions,
                x,                     //x,
                y,                     //y,
                0,                     //wheel,
                IntPtr.Zero            //extraInformation (data from user32!GetMessageExtraInfo)
            });

            //Force a GlobalHitTest. If _isSynchronize is false, it may use a LocalHitTest instead, which is a problem if we're not the foreground window
            //report.GetType().GetFieldInfo("_isSynchronize").SetValue(report, true);
            isSynchronizeFieldInfo.SetValue(report, true);

            var eventArgs = (InputEventArgs) inputReportEventArgsCtor.Invoke(new []{null, report});
            eventArgs.RoutedEvent = previewInputReportEvent;

            var oldIsFaking = isFaking;

            try
            {
                isFaking = true;

                //InputManager.Current comes from the current Dispatcher
                InputManager.Current.ProcessInput(eventArgs);
            }
            finally
            {
                isFaking = oldIsFaking;
            }
        }

        private static IntPtr HiddenWindowFromPoint(POINT pt)
        {
            /* When running unit tests, we want to open multiple windows in parallel and have each of them receive the mouse events that we
             * send to them. By default, the only window handle of interest will be the main window handle, but once we start adding in
             * more complicated WPF elements, we may have other sub-windows as well. As such, we need to do a hit test against the windows
             * in our application. In addition, we also need to be on the lookout for "special" windows (like the adorner window) which
             * will store the fact it's disabled in its extended window flags (rather than its regular flags). If we have multiple window candidates,
             * we may also even need to figure out which is the top-most window that would have actually been hit. */

            var threadHandles = new List<IntPtr>();

            //We can't use PresentationSource.CurrentSources as, when running unit tests, we could have multiple windows running at once.
            //All windows used in the current application should be listed as child windows of the main window, so constrain our search
            //to windows under the top level main window
            User32.Native.EnumThreadWindows(Kernel32.GetCurrentThreadId(), (hwnd, _) =>
            {
                threadHandles.Add(hwnd);
                return true;
            }, IntPtr.Zero);

            //There seem to be a bunch of additional random handles associated with a given WPF window,
            //but most of these won't actually have a HwndSource associated with them
            var hwndSources = threadHandles.Select(HwndSource.FromHwnd).Where(v => v != null).ToArray();

            var candidates = new List<IntPtr>();

            foreach (HwndSource source in hwndSources)
            {
                var local = pt;

                if (!User32.Native.ScreenToClient(source.Handle, ref local))
                    continue;

                if (!User32.Native.GetClientRect(source.Handle, out var rect))
                    continue;

                if (User32.Native.PtInRect(ref rect, local))
                {
                    //Even though WS_DISABLED is not an EX style, it's still located in the GWL_EXSTYLE property of the window
                    var styles = (WindowStyles) User32.Native.GetWindowLongW(source.Handle, User32.Native.GWL_EXSTYLE);

                    if (styles.HasFlag(WindowStyles.WS_DISABLED))
                        continue;

                    candidates.Add(source.Handle);
                }
            }

            if (candidates.Count != 1)
                throw new NotImplementedException($"Don't know how to handle having {candidates.Count} candidate windows"); //If it's 0, not sure what to do. If it's more than 1, we should get the top-most one

            return candidates[0];
        }
    }
}

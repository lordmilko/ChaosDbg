using System;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace ChaosDbg.Tests
{
    static class UIAutomationExtensions
    {
        private static object objLock = new object();

        //UIAutomationCore is not thread safe when it comes to initialization. CUIAutomation.ElementFromHandle will call CheckInit()
        //which will get upset if two threads attempt to call it at the same time. Therefore, we must synchronize access to the first
        //API call made into UIAutomation

        public static Window CreateWindowSafe(this UIA3Automation automation, IntPtr hwnd)
        {
            lock (objLock)
            {
                var flaWindow = automation.FromHandle(hwnd).AsWindow();

                return flaWindow;
            }
        }

        public static Window GetMainWindowSafe(this FlaUI.Core.Application application, UIA3Automation automation)
        {
            lock (objLock)
            {
                return application.GetMainWindow(automation);
            }
        }
    }
}

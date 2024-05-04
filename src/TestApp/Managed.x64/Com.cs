using System.Runtime.InteropServices;

namespace TestApp
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("C9BB051A-4842-4377-943B-3905A6047E5E")]
    public interface IExample
    {
        void Signal([In, MarshalAs(UnmanagedType.LPWStr)] string eventName);
    }

    [ComVisible(true)]
    [Guid("23FC427E-088C-472A-BD08-F8185AFDF3BD")]
    public class Example : IExample
    {
        public void Signal(string eventName)
        {
            Program.eventName = eventName;

            Program.SignalReady();
        }
    }
}

namespace ChaosDbg.WinMD
{
    /// <summary>
    /// Represents a parameter for a function pointer that itself is a parameter for some function.
    /// </summary>
    class WindowsMetadataFnPtrParameter : IWindowsMetadataParameter
    {
        public IWindowsMetadataType Type { get; }

        public WindowsMetadataFnPtrParameter(IWindowsMetadataType type)
        {
            Type = type;
        }   
    }
}

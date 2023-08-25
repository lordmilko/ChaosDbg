namespace ChaosDbg.WinMD
{
    interface IWindowsMetadataProvider
    {
        bool TryGetFunction(string name, out WindowsMetadataMethod method);
    }
}

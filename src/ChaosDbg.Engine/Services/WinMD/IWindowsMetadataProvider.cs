namespace ChaosDbg.WinMD
{
    interface IWindowsMetadataProvider
    {
        bool TryGetConstant(string name, out WindowsMetadataField constant);

        bool TryGetFunction(string name, out WindowsMetadataMethod method);
    }
}

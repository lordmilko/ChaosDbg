namespace ChaosDbg.WinMD
{
    static class WindowsMetadataProviderExtensions
    {
        public static WindowsMetadataMethod GetFunction(this IWindowsMetadataProvider provider, string name)
        {
            if (provider.TryGetFunction(name, out var method))
                return method;

            throw new System.InvalidOperationException($"Could not find any methods with name '{name}'");
        }
    }
}

namespace ChaosDbg.WinMD
{
    static class WindowsMetadataProviderExtensions
    {
        public static WindowsMetadataField GetConstant(this IWindowsMetadataProvider provider, string name)
        {
            if (provider.TryGetConstant(name, out var constant))
                return constant;

            throw new System.InvalidOperationException($"Could not find any constants with name '{name}'");
        }

        public static WindowsMetadataMethod GetFunction(this IWindowsMetadataProvider provider, string name)
        {
            if (provider.TryGetFunction(name, out var method))
                return method;

            throw new System.InvalidOperationException($"Could not find any functions with name '{name}'");
        }
    }
}

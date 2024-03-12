namespace ChaosDbg
{
    public static class ConsoleExtensions
    {
        public static void WriteLine(this IConsole console) => console.WriteLine(string.Empty);
    }
}

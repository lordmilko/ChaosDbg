namespace ChaosDbg.Theme
{
    public interface IThemeProvider
    {
        ITheme GetTheme();
    }

    class ThemeProvider : IThemeProvider
    {
        private ITheme theme = new Theme();

        public ITheme GetTheme() => theme;
    }
}

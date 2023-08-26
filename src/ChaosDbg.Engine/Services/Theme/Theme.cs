namespace ChaosDbg.Theme
{
    public interface ITheme
    {
        /// <summary>
        /// Gets the font used for displaying content within UI panes.
        /// </summary>
        Font ContentFont { get; }
    }

    class Theme : ITheme
    {
        public Font ContentFont { get; }

        public Theme()
        {
            ContentFont = new Font("Consolas", 14);
        }
    }
}

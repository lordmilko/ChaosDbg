using ChaosDbg.Theme;

namespace ChaosDbg.Render
{
    /// <summary>
    /// Represents a value that can be converted to a <see cref="IRenderable"/> form.
    /// </summary>
    public interface IConvertableToRenderable
    {
        IRenderable ToRenderable();
    }
}

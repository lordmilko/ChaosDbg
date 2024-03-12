namespace ChaosDbg.Commands
{
    public interface ICommandParser
    {
        object Parse(string value);
    }
}
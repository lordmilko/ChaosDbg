namespace ChaosDbg.Scroll
{
    public interface ILogicalScrollContent
    {
        long SeekVertical(long newOffset);

        long StepUp(int count);

        long StepDown(int count);
    }
}

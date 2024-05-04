namespace ChaosDbg.Debugger
{
    public static class DeferrableOperationStatusExtensions
    {
        public static bool IsCompleted(this DeferrableOperationStatus status)
        {
            switch (status)
            {
                case DeferrableOperationStatus.Completed:
                case DeferrableOperationStatus.Aborted:
                case DeferrableOperationStatus.Failed:
                    return true;

                default:
                    return false;
            }
        }
    }
}
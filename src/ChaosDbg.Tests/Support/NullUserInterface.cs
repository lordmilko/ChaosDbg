using System;

namespace ChaosDbg.Tests
{
    public class NullUserInterface : IUserInterface
    {
        public void HandleEvent<T>(EventHandler<T> handler, object sender, T args)
        {
            if (handler != null)
                handler.Invoke(sender, args);
        }
    }
}

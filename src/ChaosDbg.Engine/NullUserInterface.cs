using System;

namespace ChaosDbg
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

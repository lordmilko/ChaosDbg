using System;
using System.Collections.Generic;
using ClrDebug.TTD;

namespace ChaosDbg.TTD
{
    class ThreadStack
    {
        public ITtdCallFrame Current { get; private set; }

        public TtdRootFrame Root
        {
            get
            {
                var current = Current;

                while (current != null)
                {
                    if (current is TtdRootFrame r)
                        return r;

                    current = current.Parent;
                }

                return null;
            }
        }

        private ThreadInfo thread;

        public ThreadStack(ThreadInfo thread)
        {
            this.thread = thread;
        }

        public TtdCallFrame Enter(TtdCallReturnEvent @event)
        {
            var newFrame = new TtdCallFrame(@event);

            if (Current == null)
                Current = new TtdRootFrame { Thread = thread };

            newFrame.Parent = Current;

            if (Current.Children == null)
                Current.Children = new List<TtdCallFrame>();

            Current.Children.Add(newFrame);

            Current = newFrame;

            return newFrame;
        }

        public void Leave(TtdCallReturnEvent @event)
        {
            if (Current != null && !(Current is TtdRootFrame))
                Current = Current.Parent;
        }

        public void AddIndirect(TtdIndirectJumpEvent indirect)
        {
            //If we're partway through a call, not sure what to do. Synthesize a frame?
            if (Current == null)
                throw new NotImplementedException("Don't know how to handle having an indirect jump when we don't have a Current");

            if (Current.Indirects == null)
                Current.Indirects = new List<TtdIndirectJumpEvent>();

            Current.Indirects.Add(indirect);
        }
    }
}

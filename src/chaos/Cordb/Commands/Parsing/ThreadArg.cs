using System;

namespace chaos.Cordb.Commands
{
    public enum ThreadArgKind
    {
        None,
        Current,
        Event,
        Number,
        All
    }

    public struct ThreadArg
    {
        public ThreadArgKind Kind { get; }

        private int value;

        public int Value
        {
            get
            {
                if (Kind == ThreadArgKind.Number)
                    return value;

                throw new InvalidOperationException($"Cannot retrieve {nameof(ThreadArg)}.{nameof(ThreadArg.Value)} when {nameof(Kind)} is {Kind}.");
            }
        }

        public ThreadArg(ThreadArgKind kind)
        {
            if (kind == ThreadArgKind.Number)
                throw new ArgumentException(nameof(kind));

            Kind = kind;
            value = 0;
        }

        public ThreadArg(int value)
        {
            Kind = ThreadArgKind.Number;
            this.value = value;
        }

        public void Execute(
            Action none,
            Action current,
            Action<int> number,
            Action all)
        {
            switch (Kind)
            {
                case ThreadArgKind.None:
                    none();
                    break;

                case ThreadArgKind.Current:
                    current();
                    break;

                case ThreadArgKind.Number:
                    number(Value);
                    break;

                case ThreadArgKind.All:
                    all();
                    break;

                default:
                    throw new NotImplementedException($"Don't know how to handle {nameof(ThreadArgKind)} '{Kind}'.");
            }
        }

        public override string ToString()
        {
            if (Kind == ThreadArgKind.Number)
                return Value.ToString();

            return Kind.ToString();
        }
    }
}

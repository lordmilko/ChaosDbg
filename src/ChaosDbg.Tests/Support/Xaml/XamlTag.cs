namespace ChaosDbg.Tests
{
    class XamlTag
    {
        public string Value { get; }

        public XamlTagKind Kind { get; }

        public XamlTag(string value, XamlTagKind kind)
        {
            Value = value;
            Kind = kind;
        }
    }
}

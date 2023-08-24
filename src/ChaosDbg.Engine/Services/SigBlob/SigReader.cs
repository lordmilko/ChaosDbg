using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigReader
    {
        ISigMethod ReadMethod(mdMethodDef token, MetaDataImport import);

        ISigField ReadFieldType(mdFieldDef token, MetaDataImport import);

        ISigCustomAttribute ReadCustomAttribute(mdCustomAttribute token, MetaDataImport import, ITypeRefResolver typeRefResolver);
    }

    public class SigReader : ISigReader
    {
        public ISigMethod ReadMethod(mdMethodDef token, MetaDataImport import)
        {
            var info = import.GetMethodProps(token);

            var reader = new SigReaderInternal(info.ppvSigBlob, info.pcbSigBlob, token, import);

            return reader.ParseMethod(info.szMethod, true);
        }

        public ISigField ReadFieldType(mdFieldDef token, MetaDataImport import)
        {
            var info = import.GetFieldProps(token);

            var reader = new SigReaderInternal(info.ppvSigBlob, info.pcbSigBlob, token, import);

            return reader.ParseField(info.szField);
        }

        public ISigCustomAttribute ReadCustomAttribute(mdCustomAttribute token, MetaDataImport import, ITypeRefResolver typeRefResolver)
        {
            var info = import.GetCustomAttributeProps(token);

            var reader = new SigReaderInternal(info.ppBlob, info.pcbSize, token, import);

            return reader.ParseCustomAttribute(info.ptkType, typeRefResolver);
        }
    }
}

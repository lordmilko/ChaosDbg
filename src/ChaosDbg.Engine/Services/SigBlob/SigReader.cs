using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigReader
    {
        ISigMethod ReadMethod(mdMethodDef token, MetaDataImport import);

        ISigField ReadFieldType(mdFieldDef token, MetaDataImport import);
    }

    public class SigReader : ISigReader
    {
        public ISigMethod ReadMethod(mdMethodDef token, MetaDataImport import)
        {
            var info = import.GetMethodProps(token);

            var reader = new SigReaderInternal(info.ppvSigBlob, info.pcbSigBlob, token, import);

            return reader.ParseMethod(info.szMethod, false);
        }

        public ISigField ReadFieldType(mdFieldDef token, MetaDataImport import)
        {
            var info = import.GetFieldProps(token);

            var reader = new SigReaderInternal(info.ppvSigBlob, info.pcbSigBlob, token, import);

            return reader.ParseField(info.szField);
        }
    }
}

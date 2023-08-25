using ClrDebug;

namespace ChaosDbg.Metadata
{
    public interface ISigReader
    {
        ISigMethod ReadMethod(mdMethodDef token, MetaDataImport import, MetaDataImport_GetMethodPropsResult? props = null);

        ISigField ReadField(mdFieldDef token, MetaDataImport import, GetFieldPropsResult? props = null);

        ISigCustomAttribute ReadCustomAttribute(mdCustomAttribute token, MetaDataImport import, ITypeRefResolver typeRefResolver);
    }

    public class SigReader : ISigReader
    {
        public ISigMethod ReadMethod(mdMethodDef token, MetaDataImport import, MetaDataImport_GetMethodPropsResult? props = null)
        {
            var info = props ?? import.GetMethodProps(token);

            var reader = new SigReaderInternal(info.ppvSigBlob, info.pcbSigBlob, token, import);

            return reader.ParseMethod(info.szMethod, true);
        }

        public ISigField ReadField(mdFieldDef token, MetaDataImport import, GetFieldPropsResult? props = null)
        {
            var info = props ?? import.GetFieldProps(token);

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

using ClrDebug;

namespace ChaosDbg.Cordb
{
    static class CordbFormatter
    {
        public static string FunctionName(CorDebugFunction function)
        {
            var module = function.Module;
            var mdi = module.GetMetaDataInterface<MetaDataImport>();

            var methodProps = mdi.GetMethodProps(function.Token);
            var classProps = mdi.GetTypeDefProps(methodProps.pClass);

            var name = $"{classProps.szTypeDef}.{methodProps.szMethod}";

            return name;
        }
    }
}

namespace ChaosDbg.DbgEng
{
    /// <summary>
    /// Represents a module that has been loaded into a DbgEng debugger.
    /// </summary>
    public class DbgEngModule
    {
        public long BaseAddress { get; }

        public string FileName { get; }

        public string ModuleName { get; }

        public int ModuleSize { get; }

        public long EndAddress => BaseAddress + ModuleSize;

        public DbgEngModule(long baseAddress, string fileName, string moduleName, int moduleSize)
        {
            BaseAddress = baseAddress;
            FileName = fileName;
            ModuleName = moduleName;
            ModuleSize = moduleSize;
        }

        public override string ToString()
        {
            return ModuleName;
        }
    }
}

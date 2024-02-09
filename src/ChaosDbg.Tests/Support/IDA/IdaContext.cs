namespace ChaosDbg.Tests
{
    class IdaContext
    {
        public int FunctionIndex { get; set; }

        public int InstructionIndex { get; set; }

        public IDAFunctionMetadata CurrentFunction => idaRoutines[FunctionIndex] as IDAFunctionMetadata;

        public IDACollapsedFunction CurrentCollapsedFunction => idaRoutines[FunctionIndex] as IDACollapsedFunction;

        public IDAFunctionMetadata NextFunction => (IDAFunctionMetadata) idaRoutines[FunctionIndex + 1];

        public IDALine CurrentInstruction => Instructions[InstructionIndex];

        public IDALine[] Instructions => CurrentFunction.Code;

        public int Length => idaRoutines.Length;

        private IDAMetadata[] idaRoutines;

        public IdaContext(IDAMetadata[] idaRoutines)
        {
            this.idaRoutines = idaRoutines;
        }
    }
}

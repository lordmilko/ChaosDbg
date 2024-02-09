using System;

namespace ChaosDbg.Analysis
{
    [Flags]
    enum FoundBySubType
    {
        //Subtypes for FoundBy. Has no effect on priority and is for informational purposes only

        None = 1,
        GuardCFFunctionTable = 2,
        GuardEHContinuationTable = 4,
        ScopeRecordBegin = 5,
        ScopeRecordEnd = 6,
        ScopeRecordHandler = 7,
        ScopeRecordJumpTarget = 8
    }

    [Flags]
    enum FoundBy
    {
        //Items in this enum are ordered by how "trustworthy" a given item is

        //These items should not be used for normal function discovery.
        
        //Imports shouldn't be treated as function calls
        Import = 1,

        RVA = 2,
        
        //Normal types

        //XFG is very prone to treating "good bytes" as "bad". We solve this a little by doing a post analysis
        //against the matched patterns
        XfgPattern = 4,

        Pattern = 8,

        //Config items can either point to data or functions. Items that are known to be about functions will be added to
        //the functions queue
        Config = 16,

        //Possibly a jump to an external function, possibly we had a bug in trying to locate a chunk containing a jmp
        //within a function
        ExternalJmp = 32,

        //Calls are as reliable as the source they came from. Thus, calls from UnwindData are more reliable than calls
        //from patterns. However, if we fail to detect a potential noreturn, we might start disassembling into junk
        //and collect calls which aren't actually valid. It's better that we handle other things we have some kind of indication
        //are more valid looking before any of these suspicious calls
        Call = 64,

        //You can have exports that aren't functions. This seems more likely than having bad symbols
        Export = 128,

        //You can have symbols that actually refer to globals
        Symbol = 256,

        UnwindInfo = 512,

        FHandler = 1024,

        //All unwind data should refer to functions, or at the very least valid memory addresses part way through functions
        RuntimeFunction = 2048
    }
}

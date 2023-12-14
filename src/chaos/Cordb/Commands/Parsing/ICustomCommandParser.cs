using System;

namespace chaos.Cordb.Commands
{
    interface ICustomCommandParser
    {
        Action Parse(ArgParser args);
    }
}

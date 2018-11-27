using System;

namespace Mocosha.DbProvider
{
    public class CommandNotCreatedException : Exception
    {
        public CommandNotCreatedException(string message) 
            : base(message)
        {
            
        }
    }
}

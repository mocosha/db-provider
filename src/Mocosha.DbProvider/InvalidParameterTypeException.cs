using System;

namespace Mocosha.DbProvider
{
    public class InvalidParameterTypeException : Exception
    {
        public InvalidParameterTypeException(string message) 
            : base(message)
        {
            
        }
    }
}

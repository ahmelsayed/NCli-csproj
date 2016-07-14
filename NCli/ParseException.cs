using System;

namespace NCli
{
    public class ParseException : Exception
    {
        public ParseException(string message) : base(message)
        { }
    }
}

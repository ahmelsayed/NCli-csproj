using System;
using static NCli.Constants;

namespace NCli
{
    public class OptionAttribute : Attribute
    {
        public object DefaultValue { get; set; }
        public string HelpText { get; set; }
        internal char _shortName { get; }
        internal string _longName { get; }
        internal int _order { get; }

        public OptionAttribute(char shortName, string longName)
        {
            _shortName = shortName;
            _longName = longName;
            _order = -1;
        }

        public OptionAttribute(string longName) : this(NullCharacter, longName)
        { }

        public OptionAttribute(char shortName) : this(shortName, string.Empty)
        { }

        public OptionAttribute(int order) : this(NullCharacter, string.Empty)
        {
            _order = order;
        }
    }
}

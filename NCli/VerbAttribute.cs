using System;

namespace NCli
{
    public class VerbAttribute : Attribute
    {
        public string HelpText { get; set; }
        public string Usage { get; set; }
        public bool Show { get; set; } = true;
        internal string[] Names { get; }
        public VerbAttribute(params string[] names)
        {
            Names = names;
        }
    }
}

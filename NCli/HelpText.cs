using System.Collections.Generic;

namespace NCli
{
    public class HelpText : List<HelpLine>
    {
        public HelpText(IEnumerable<HelpLine> lines) : base(lines)
        {
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G.Configuration.Options
{
    public class ConditionOption
    {
        public ConditionEnum Condition { get; set; }
        public string ValueInCondition { get; set; }
        public string NewValue { get; set; }

    }
}

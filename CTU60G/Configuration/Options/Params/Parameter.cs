using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G.Configuration.Options
{
    public class Parameter
    {
        public string DefaultValue { get; set; }
        public List<ConditionOption> Conditions { get; set; }
    }
}

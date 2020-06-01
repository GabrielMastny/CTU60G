using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G.Configuration.Options
{
    public abstract class MutualParameters
    {
        public Parameter Freq { get; set; }
        public Parameter Name { get; set; }
        public Parameter Volume { get; set; }
        public Parameter ChannelWidth { get; set; }
        public Parameter Power { get; set; }
        public Parameter SN { get; set; }
        public Parameter Mac { get; set; }
    }
}

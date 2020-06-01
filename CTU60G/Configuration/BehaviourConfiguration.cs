using CTU60G.Configuration;
using CTU60G.Options;
using CTU60G.Options.Behaviour;
using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G
{
    public class BehaviourConfiguration : IBehaviourConfiguration
    {
        public P2pParameters p2p { get; set;}
        public WigigP2pOptions wp2p { get; set; }
        public WigigP2MPOptions wp2mp { get; set; }
    }
}

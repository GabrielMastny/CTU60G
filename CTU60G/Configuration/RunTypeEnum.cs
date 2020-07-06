using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G.Configuration
{
    public enum RunTypeEnum
    {
        Once = 1,
        InLoop = 2,
        InSpecifedTime = 4,
        AfterSpecifedTime = 8
    }
}

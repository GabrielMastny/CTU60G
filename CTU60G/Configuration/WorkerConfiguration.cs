using CTU60G.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G
{
    public class WorkerConfiguration : IWorkerConfiguration
    {
        public string DataURLOrFilePath { get; set; }
        public string CTULogin { get; set; }
        public string CTUPass { get; set; }
        public bool SignalIsolationConsentIfMyOwn { get; set; }
        public string ResponseWithCTUID { get; set; }
        public string ResponseOnDelete { get; set; }
    }
}

using CTU60G.Configuration;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text;

namespace CTU60G
{
    public class WorkerConfiguration
    {
        public string DataURLOrFilePath { get; set; }
        public string CTULogin { get; set; }
        public string CTUPass { get; set; }
        public bool SignalIsolationConsentIfMyOwn { get; set; }
        public string ResponseWithCTUID { get; set; }
        public string ResponseOnDelete { get; set; }
        public string Synchronization
        {
            get => synchronization.ToString();
            set
            {
                object val = null;
                if (Enum.TryParse(typeof(SynchronizationTypeEnum), value, out val)) synchronization = (SynchronizationTypeEnum)val;
            }
        }
        private SynchronizationTypeEnum? synchronization;
        public string Run
        {
            get => run.ToString();
            set
            {
                object val = null;
                if (Enum.TryParse(typeof(RunTypeEnum), value, out val)) run = (RunTypeEnum)val;
            }
        }
        private RunTypeEnum run;
        public TimeSpan RunTimeSpecification {get;set;}


    }
}

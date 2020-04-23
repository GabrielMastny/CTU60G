namespace CTU60G.Json
{
    using System;
    using System.Collections.Generic;

    using System.Globalization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public partial class WirelessSite
    {
        [JsonProperty("infos", Required = Required.Always)]
        public WirelessSiteInfo Infos { get; set; }

        [JsonProperty("stations", NullValueHandling = NullValueHandling.Ignore)]
        public List<WirelessUnit> Stations { get; set; }

        [JsonProperty("ap", Required = Required.Always)]
        public List<WirelessUnit> Ap { get; set; }
    }

    public partial class WirelessUnit
    {
        [JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty("lat", NullValueHandling = NullValueHandling.Ignore)]
        public string Lat { get; set; }

        [JsonProperty("lon", NullValueHandling = NullValueHandling.Ignore)]
        public string Lon { get; set; }

        [JsonProperty("freq", NullValueHandling = NullValueHandling.Ignore)]
        public string Freq { get; set; }

        [JsonProperty("antena_gain", NullValueHandling = NullValueHandling.Ignore)]
        public string AntennaGain { get; set; }

        [JsonProperty("power", NullValueHandling = NullValueHandling.Ignore)]
        public string Power { get; set; }

        [JsonProperty("channel_width", NullValueHandling = NullValueHandling.Ignore)]
        public string ChannelWidth { get; set; }

        [JsonProperty("rsn", NullValueHandling = NullValueHandling.Ignore)]
        public string RSN { get; set; }

        [JsonProperty("azimut", NullValueHandling = NullValueHandling.Ignore)]
        public string Azimut { get; set; }

        [JsonProperty("eirp", NullValueHandling = NullValueHandling.Ignore)]
        public string EIRP { get; set; }

        [JsonProperty("mac_addr", NullValueHandling = NullValueHandling.Ignore)]
        public string MacAddr { get; set; }

        [JsonProperty("sn", NullValueHandling = NullValueHandling.Ignore)]
        public string SerialNumber { get; set; }

        [JsonProperty("mode", NullValueHandling = NullValueHandling.Ignore)]
        public string Mode { get; set; }

        [JsonProperty("ctu_reported", NullValueHandling = NullValueHandling.Ignore)]
        public string CtuReported { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }
    }

    public partial class WirelessSiteInfo
    {
        [JsonProperty("ssid", NullValueHandling = NullValueHandling.Ignore)]
        public string Ssid { get; set; }

        [JsonProperty("subnet_id", NullValueHandling = NullValueHandling.Ignore)]
        public string SubnetId { get; set; }

        [JsonProperty("site_type", NullValueHandling = NullValueHandling.Ignore)]
        public string SiteType { get; set; }

    }
}
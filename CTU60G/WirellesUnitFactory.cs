using CTU60G.Json;
using CTU60GLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CTU60G
{
    public static class WirellesUnitFactory
    {
        public static FixedP2PPair CreatePTP(WirelessSite site)
        {
            WirelessUnit ap = site.Ap.FirstOrDefault();
            WirelessUnit station = site.Stations.FirstOrDefault();
            FixedStationInfo a = new FixedStationInfo(ap.Name, ap.SerialNumber, ap.MacAddr, ap.Lon, ap.Lat, ap.AntennaGain, ap.ChannelWidth, ap.Power, ap.Freq, ap.RSN);
            FixedStationInfo b = new FixedStationInfo(station.Name, station.SerialNumber, station.MacAddr, station.Lon, station.Lat, station.AntennaGain, station.ChannelWidth, station.Power, station.Freq, station.RSN);
            return new FixedP2PPair(a, b);
        }

        public static WigigPTMPUnitInfo CreateWigigPTMP(WirelessSite site)
        {
            WirelessUnit ap = site.Ap.FirstOrDefault();
            return new WigigPTMPUnitInfo(ap.Name, ap.SerialNumber, ap.MacAddr, ap.Lon, ap.Lat, ap.AntennaGain, ap.ChannelWidth, ap.Power, ap.Freq, ap.EIRP, ap.Azimut);
        }

    }
}

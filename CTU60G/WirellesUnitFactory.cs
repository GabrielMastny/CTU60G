using CTU60G.Configuration;
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
        public static BehaviourConfiguration Behaviour;

        public static List<P2PSite> CreatePTP(WirelessSite site)
        {
            WirelessUnit ap = site.Ap.FirstOrDefault();
            List<P2PSite> pairs = new List<P2PSite>();
            if(site.Stations != null)
            {
                foreach (var stat in site.Stations)
                {
                    FixedStationInfo a = ApplyBehaviour(ap);
                    FixedStationInfo b = ApplyBehaviour(stat);
                    pairs.Add(new P2PSite(a, b));
                }
            }
            
            return pairs;
        }

        private static FixedStationInfo ApplyBehaviour(WirelessUnit unit)
        {
            string name = String.IsNullOrEmpty(unit?.Name) ? Behaviour?.p2p?.Name?.DefaultValue : unit.Name ;
            name = "";
            string serialNumber = String.IsNullOrEmpty(unit?.SerialNumber) ? Behaviour?.p2p?.SN?.DefaultValue : unit.SerialNumber;
            string macAddress = String.IsNullOrEmpty(unit?.MacAddr) ? Behaviour?.p2p?.Mac?.DefaultValue : unit.MacAddr;
            string longitude = unit.Lon;
            string latitude = unit.Lat;
            string gain = String.IsNullOrEmpty(unit?.AntennaGain) ? Behaviour?.p2p?.Volume?.DefaultValue : unit.AntennaGain;
            string channelWidth = String.IsNullOrEmpty(unit?.ChannelWidth) ? Behaviour?.p2p?.ChannelWidth?.DefaultValue : unit.ChannelWidth;
            string power = String.IsNullOrEmpty(unit?.Power) ? Behaviour?.p2p?.Power?.DefaultValue : unit.Power;
            string frequency = String.IsNullOrEmpty(unit?.Freq) ? Behaviour?.p2p?.Freq?.DefaultValue : unit.Freq;
            string rsn = String.IsNullOrEmpty(unit?.RSN) ? Behaviour?.p2p?.Rsn?.DefaultValue : unit.RSN;
            string ctuId = unit.CtuReported;
            string ownerId = unit.Id;

            
            return new FixedStationInfo(name, serialNumber, macAddress, longitude, latitude, gain, channelWidth, power, frequency, ctuId, ownerId, rsn);

        }
        public static WigigPTMPUnitInfo CreateWigigPTMP(WirelessSite site)
        {
            WirelessUnit ap = site.Ap.FirstOrDefault();
            return new WigigPTMPUnitInfo(ap.Name, ap.SerialNumber, ap.MacAddr, ap.Lon, ap.Lat, ap.AntennaGain, ap.ChannelWidth, ap.Power, ap.Freq, ap.EIRP, ap.Azimut);
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace JorJika.Api.ServiceRegistry.Consul.Helpers
{
    public static class IPHelper
    {
        public static string GetLocalIp()
        {
            var ipaddress = "";
            var firstUpInterface = NetworkInterface.GetAllNetworkInterfaces()
                                                    .OrderBy(x => x.NetworkInterfaceType.ToString())
                                                    //.OrderByDescending(c => c.Speed)
                                                    .FirstOrDefault(c => c.NetworkInterfaceType != NetworkInterfaceType.Loopback
                                                                     && (c.NetworkInterfaceType == NetworkInterfaceType.Ethernet || c.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                                                                     && !c.Name.Contains("bluetooth")
                                                                     && c.OperationalStatus == OperationalStatus.Up
                                                                     );
            if (firstUpInterface != null)
            {
                var props = firstUpInterface.GetIPProperties();
                // get first IPV4 address assigned to this interface
                var firstIpV4Address = props.UnicastAddresses
                    .Where(c => c.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(c => c.Address)
                    .FirstOrDefault();

                if (firstIpV4Address != null)
                    ipaddress = firstIpV4Address.ToString();
            }

            return ipaddress;
        }
    }
}

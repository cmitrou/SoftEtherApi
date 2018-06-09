﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using SoftEtherApi.Extensions;
using SoftEtherApi.Model;
using SoftEtherApi.SoftEtherModel;

namespace SoftEtherApi.Infrastructure
{
    public static class AccessListFactory
    {
        private const uint DhcpPriority = 1000;
        private const uint GatewayNatPriority = 2000;
        private const uint NetworkNatPriority = 3000;
        public const uint DenyDevicesPriority = 4000;
        public const uint AllowDevicesPriority = 5000;
        public const uint CatchAllPriority = 10000;
        
        private const string AccessFromDeviceString = "AccessFromDevice";
        private const string AccessToDeviceString = "AccessToDevice";
        
        private const string AccessFromNetworkString = "AccessFromNetwork";
        private const string AccessToNetworkString = "AccessToNetwork";

        private const string NatGatewayString = "NatGateway";
        private const string NatNetworkString = "NAT-Network";
        
        public static HubAccessList Dhcp(uint priority, string name = "DHCP", bool denyAccess = false)
        {
            var accessList = new HubAccessList
            {
                Active = true,
                Priority = priority,
                DestIpAddress = IPAddress.Broadcast,
                DestSubnetMask = IPAddress.Broadcast,
                Protocol = 17,
                DestPortStart = 67,
                DestPortEnd = 68,
                Discard = denyAccess,
                Note = name
            };
            return accessList;
        }

        public static HubAccessList CatchAll(uint priority, string name = "Catch ALL", bool denyAccess = false)
        {
            var accessList = new HubAccessList
            {
                Active = true,
                Priority = priority,
                Discard = denyAccess,
                Note = name
            };
            return accessList;
        }

        public static IEnumerable<HubAccessList> AccessToDevice(uint priority, string name,
            IPAddress device,
            IPAddress network, IPAddress networkSubnet, bool denyAccess = false)
        {
            return new List<HubAccessList>
            {
                new HubAccessList
                {
                    Active = true,
                    Priority = priority,
                    SrcIpAddress = device,
                    SrcSubnetMask = IPAddress.Broadcast,
                    DestIpAddress = network.GetNetworkAddress(networkSubnet),
                    DestSubnetMask = networkSubnet,
                    Discard = denyAccess,
                    Note = $"{AccessFromDeviceString}-{name}"
                },
                new HubAccessList
                {
                    Active = true,
                    Priority = priority,
                    SrcIpAddress = network.GetNetworkAddress(networkSubnet),
                    SrcSubnetMask = networkSubnet,
                    DestIpAddress = device,
                    DestSubnetMask = IPAddress.Broadcast,
                    Discard = denyAccess,
                    Note = $"{AccessToDeviceString}-{name}"
                }
            };
        }
        
        public static IEnumerable<HubAccessList> AccessToNetwork(uint priority, string name,
            IPAddress network, IPAddress networkSubnet,
            IPAddress otherNetwork, IPAddress otherNetworkSubnet, bool denyAccess = false)
        {
            return new List<HubAccessList>
            {
                new HubAccessList
                {
                    Active = true,
                    Priority = priority,
                    SrcIpAddress = network.GetNetworkAddress(networkSubnet),
                    SrcSubnetMask = networkSubnet,
                    DestIpAddress = otherNetwork.GetNetworkAddress(otherNetworkSubnet),
                    DestSubnetMask = otherNetworkSubnet,
                    Discard = denyAccess,
                    Note = $"{AccessFromNetworkString}-{name}"
                },
                new HubAccessList
                {
                    Active = true,
                    Priority = priority,
                    SrcIpAddress = otherNetwork.GetNetworkAddress(otherNetworkSubnet),
                    SrcSubnetMask = otherNetworkSubnet,
                    DestIpAddress = network.GetNetworkAddress(networkSubnet),
                    DestSubnetMask = networkSubnet,
                    Discard = denyAccess,
                    Note = $"{AccessToNetworkString}-{name}"
                }
            };
        }

        public static IEnumerable<HubAccessList> FilterDevicesOnly(IEnumerable<HubAccessList> accessLists)
        {
            return accessLists.Where(m => m.Priority == AllowDevicesPriority || m.Priority == DenyDevicesPriority).ToList();
        }
        
        public static IEnumerable<IPAddress> GetDevicesOnlyIps(IEnumerable<HubAccessList> accessLists)
        {
            var filtered = FilterDevicesOnly(accessLists);
            return filtered.Where(m => m.Note.StartsWith(AccessFromDeviceString))
                .Select(m => m.SrcIpAddress).ToList();
        }

        public static List<HubAccessList> ReplaceDevices(IEnumerable<HubAccessList> accessLists, params AccessDevice[] accessDevices)
        {
            //we need the gatewayAccess rule
            var gatewayAccess = accessLists.Where(m => !string.IsNullOrWhiteSpace(m.Note))
                .Single(m => m.Note.StartsWith(AccessFromDeviceString) && m.Note.EndsWith(NatGatewayString));
            
            var newList = accessLists.Except(FilterDevicesOnly(accessLists)).ToList();
            newList.AddRange(accessDevices.SelectMany(m => AccessToDevice(AllowDevicesPriority, m.Name, m.Ip, gatewayAccess.SrcIpAddress, gatewayAccess.DestSubnetMask)));
            return newList;
        }

        public static List<HubAccessList> AllowNetworkOnly(
            string network, string networkSubnet,
            IPAddress secureNatGateway, IPAddress secureNatSubnet)
        {
            return AllowNetworkOnly(IPAddress.Parse(network), IPAddress.Parse(networkSubnet), secureNatGateway,
                secureNatSubnet);
        }

        public static List<HubAccessList> AllowNetworkOnly(
            IPAddress network, IPAddress networkSubnet,
            IPAddress secureNatGateway, IPAddress secureNatSubnet)
        {
            var result = new List<HubAccessList>
            {
                Dhcp(DhcpPriority),
                CatchAll(CatchAllPriority, denyAccess: true)
            };
            
            result.AddRange(AccessToDevice(GatewayNatPriority, NatGatewayString, secureNatGateway, secureNatGateway, secureNatSubnet));
            result.AddRange(AccessToNetwork(NetworkNatPriority, NatNetworkString, network, networkSubnet, secureNatGateway, secureNatSubnet));

            return result;
        }

        public static List<HubAccessList> AllowDevicesOnly(IPAddress secureNatGateway, IPAddress secureNatSubnet, 
            params AccessDevice[] accessDevices)
        {
            var result = new List<HubAccessList>
            {
                Dhcp(DhcpPriority),
                CatchAll(CatchAllPriority, denyAccess: true)
            };
            
            result.AddRange(AccessToDevice(GatewayNatPriority, NatGatewayString, secureNatGateway, secureNatGateway, secureNatSubnet));
            result.AddRange(accessDevices.SelectMany(m => AccessToDevice(AllowDevicesPriority, m.Name, m.Ip, secureNatGateway, secureNatSubnet)));
            
            return result;
        }
    }
}
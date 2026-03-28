using AttendanceShiftingManagement.Services;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;

namespace AttendanceShiftingManagement.Tests;

public sealed class LocalScannerGatewayNetworkTests
{
    [Fact]
    public void ResolveLanAddress_PrefersGatewayBackedNonVirtualIpv4Address()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(item =>
                item.OperationalStatus == OperationalStatus.Up &&
                item.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(item =>
            {
                var properties = item.GetIPProperties();
                var hasGateway = properties.GatewayAddresses.Any(gateway =>
                    gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                    gateway.Address.ToString() != "0.0.0.0");

                return properties.UnicastAddresses
                    .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(address => new LanAddressCandidate(
                        address.Address.ToString(),
                        item.Name,
                        item.Description,
                        item.NetworkInterfaceType,
                        hasGateway));
            })
            .ToList();

        var expected = candidates
            .OrderBy(GetPriority)
            .ThenBy(candidate => candidate.Address, StringComparer.Ordinal)
            .Select(candidate => candidate.Address)
            .FirstOrDefault() ?? "127.0.0.1";

        var method = typeof(LocalScannerGatewayService).GetMethod(
            "ResolveLanAddress",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var actual = Assert.IsType<string>(method!.Invoke(null, Array.Empty<object>()));
        Assert.Equal(expected, actual);
    }

    private static int GetPriority(LanAddressCandidate candidate)
    {
        var score = 0;

        if (candidate.IsApipa)
        {
            score += 1000;
        }

        if (!candidate.HasGateway)
        {
            score += 100;
        }

        if (candidate.IsVirtualLike)
        {
            score += 50;
        }

        if (!candidate.IsPreferredInterfaceType)
        {
            score += 10;
        }

        return score;
    }

    private sealed record LanAddressCandidate(
        string Address,
        string Name,
        string Description,
        NetworkInterfaceType InterfaceType,
        bool HasGateway)
    {
        private static readonly string[] VirtualInterfaceKeywords =
        {
            "virtual",
            "hyper-v",
            "default switch",
            "virtualbox",
            "host-only",
            "vmware",
            "docker",
            "wsl",
            "vpn",
            "tunnel"
        };

        public bool IsApipa => Address.StartsWith("169.254.", StringComparison.Ordinal);

        public bool IsPreferredInterfaceType =>
            InterfaceType == NetworkInterfaceType.Wireless80211 ||
            InterfaceType == NetworkInterfaceType.Ethernet ||
            InterfaceType == NetworkInterfaceType.GigabitEthernet ||
            InterfaceType == NetworkInterfaceType.FastEthernetFx ||
            InterfaceType == NetworkInterfaceType.FastEthernetT;

        public bool IsVirtualLike
        {
            get
            {
                var haystack = $"{Name} {Description}";
                return VirtualInterfaceKeywords.Any(keyword => haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}

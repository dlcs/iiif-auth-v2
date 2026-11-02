using System.Net;
using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Tests.Models.Domain;

public class IpRestrictionConfigurationTests
{
    private static IpRestrictionConfiguration GetConfig(params string[] ranges)
        => new() { Config = RoleProviderType.IpRestriction, Ranges = ranges };

    [Theory]
    [InlineData("10.0.0.0")]
    [InlineData("10.0.0.1")]
    [InlineData("10.0.0.255")]
    public void Contains_True_IfIPv4InRange(string ip)
    {
        var sut = GetConfig("10.0.0.0/24");

        sut.Contains(IPAddress.Parse(ip)).Should().BeTrue();
    }

    [Theory]
    [InlineData("10.0.1.0")]
    [InlineData("192.168.0.1")]
    [InlineData("2001:db8:1007::1")]
    public void Contains_False_IfIPv4NotInRange(string ip)
    {
        var sut = GetConfig("10.0.0.0/24");

        sut.Contains(IPAddress.Parse(ip)).Should().BeFalse();
    }

    [Theory]
    [InlineData("2001:db8:1007::1", true)]
    [InlineData("2001:db8:1007:ffff::1", true)]
    [InlineData("2001:db8:1008::1", false)]
    [InlineData("10.0.0.1", false)]
    public void Contains_HandlesIPv6Range(string ip, bool expected)
    {
        var sut = GetConfig("2001:db8:1007::/48");

        sut.Contains(IPAddress.Parse(ip)).Should().Be(expected);
    }

    [Fact]
    public void Contains_True_IfIPv4MappedToIPv6InRange()
    {
        var sut = GetConfig("10.0.0.0/24");

        sut.Contains(IPAddress.Parse("::ffff:10.0.0.5")).Should().BeTrue();
    }

    [Fact]
    public void Contains_True_IfAnyRangeMatches()
    {
        var sut = GetConfig("192.168.0.0/16", "10.0.0.0/24");

        sut.Contains(IPAddress.Parse("10.0.0.5")).Should().BeTrue();
    }

    [Fact]
    public void Contains_IgnoresInvalidRanges()
    {
        var sut = GetConfig("not-a-range", "10.0.0.0/99", "10.0.0.0/24");

        sut.Contains(IPAddress.Parse("10.0.0.5")).Should().BeTrue();
    }

    [Fact]
    public void Contains_False_IfNoRanges()
    {
        var sut = GetConfig();

        sut.Contains(IPAddress.Parse("10.0.0.5")).Should().BeFalse();
    }

    [Fact]
    public void Contains_False_IfIpNull()
    {
        var sut = GetConfig("10.0.0.0/24");

        sut.Contains(null).Should().BeFalse();
    }

    [Fact]
    public void Contains_UsesUpdatedRanges_IfRangesChanged()
    {
        var sut = GetConfig("10.0.0.0/24");
        sut.Contains(IPAddress.Parse("10.0.0.5")).Should().BeTrue();

        sut.Ranges = ["192.168.0.0/16"];

        sut.Contains(IPAddress.Parse("10.0.0.5")).Should().BeFalse();
    }
}

using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Tests.Models.Domain;

public class RoleXTests
{
    [Theory]
    [InlineData("https://api.dlcs.io/customers/2/roles/ip:reading-room")]
    [InlineData("https://api.dlcs.io/customers/2/roles/ip:reading-room/")]
    [InlineData("ip:reading-room")]
    public void IsIpRole_True_IfLastSegmentHasIpPrefix(string role)
        => role.IsIpRole().Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://api.dlcs.io/customers/2/roles/clickthrough")]
    [InlineData("https://api.dlcs.io/customers/2/roles/appointment:token-based")]
    [InlineData("https://api.dlcs.io/customers/2/ip:roles/reading-room")]
    [InlineData("https://api.dlcs.io/customers/2/roles/IP:reading-room")]
    [InlineData("https://api.dlcs.io/customers/2/roles/staff-ip:reading-room")]
    public void IsIpRole_False_IfLastSegmentDoesNotHaveIpPrefix(string? role)
        => role.IsIpRole().Should().BeFalse();
}

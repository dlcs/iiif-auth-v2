using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Tests.Models.Domain;

public class RoleEntryTests
{
    [Theory]
    [InlineData("a", new[] { "a" })]
    [InlineData("a+b", new[] { "a", "b" })]
    [InlineData("a b", new[] { "a", "b" })]
    [InlineData(" a ++ b+", new[] { "a", "b" })]
    [InlineData("a+b+a", new[] { "a", "b" })]
    [InlineData("http://dlcs.test/99/ip:reading-room+http://dlcs.test/99/staff",
        new[] { "http://dlcs.test/99/ip:reading-room", "http://dlcs.test/99/staff" })]
    public void Parse_SplitsOnPlusAndWhitespace(string entry, string[] expected)
        => RoleEntry.Parse(entry).Roles.Should().Equal(expected);

    [Fact]
    public void ParseList_SplitsEntriesOnComma()
        => RoleEntry.ParseList("a,b+c,d e").Should().Equal(
            new RoleEntry(["a"]), new RoleEntry(["b", "c"]), new RoleEntry(["d", "e"]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(", ,+,")]
    public void ParseList_Empty_IfNoRoles(string? entries)
        => RoleEntry.ParseList(entries).Should().BeEmpty();

    [Fact]
    public void ToString_RoundTrips()
    {
        var entry = RoleEntry.Parse("a+b");
        entry.ToString().Should().Be("a+b");
        RoleEntry.Parse(entry.ToString()).Should().Be(entry);
    }

    [Fact]
    public void AllRoles_DistinctAcrossEntries()
        => RoleEntry.ParseList("a+b,b+c,a").AllRoles().Should().Equal("a", "b", "c");
}

using IIIFAuth2.Management.Utils;

namespace IIIFAuth2.Management.Tests.Utils;

public class DateTimeXTests
{
    [Fact]
    public void ToUtc_AssumesUtc_IfUnspecified()
    {
        var value = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Unspecified);

        var result = value.ToUtc();

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Ticks.Should().Be(value.Ticks);
    }

    [Fact]
    public void ToUtc_Converts_IfLocal()
    {
        var value = new DateTime(2026, 7, 1, 9, 0, 0, DateTimeKind.Local);

        value.ToUtc().Should().Be(value.ToUniversalTime());
    }
}

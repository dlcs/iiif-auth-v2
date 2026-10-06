using IIIFAuth2.API.Models.Domain;

namespace IIIFAuth2.API.Tests.Models.Domain;

public class DeliverableIdTests
{
    [Fact]
    public void ToString_CorrectFormat_Asset()
    {
        var deliverableId = new DeliverableId(19, 4, "my-first-image");
        const string expected = "19/4/my-first-image";

        deliverableId.ToString().Should().Be(expected);
    }

    [Fact]
    public void ToString_CorrectFormat_Adjunct()
    {
        var deliverableId = new DeliverableId(19, 4, "my-first-image", "adjunct-1");
        const string expected = "19/4/my-first-image/adjunct-1";

        deliverableId.ToString().Should().Be(expected);
    }

    [Fact]
    public void FromString_ReturnsExpected_Asset()
    {
        var deliverableId = DeliverableId.FromString("19/4/my-first-image");

        deliverableId.Customer.Should().Be(19);
        deliverableId.Space.Should().Be(4);
        deliverableId.Asset.Should().Be("my-first-image");
        deliverableId.AdjunctId.Should().BeNull();
    }

    [Fact]
    public void FromString_ReturnsExpected_Adjunct()
    {
        var deliverableId = DeliverableId.FromString("19/4/my-first-image/adjunct-1");

        deliverableId.Customer.Should().Be(19);
        deliverableId.Space.Should().Be(4);
        deliverableId.Asset.Should().Be("my-first-image");
        deliverableId.AdjunctId.Should().Be("adjunct-1");
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("foo/bar")]
    [InlineData("foo/bar/baz")]
    [InlineData("1/2")]
    [InlineData("1/2/asset/adjunct/extra")]
    public void FromString_Throws_IfInvalidFormat(string candidate)
    {
        Action action = () => DeliverableId.FromString(candidate);
        action.Should()
            .Throw<FormatException>()
            .WithMessage(
                $"DeliverableId '{candidate}' is invalid. Must be in format customer/space/asset or customer/space/asset/adjunct");
    }

    [Theory]
    [InlineData("99/100/foo", "99/100/foo", true)]
    [InlineData("99/100/foo/bar", "99/100/foo/bar", true)]
    [InlineData("99/100/foo", "99/100/foo/bar", false)]
    [InlineData("99/100/foo/bar", "99/100/foo/baz", false)]
    public void EqualsOperator_Compares_Values(string one, string two, bool expected)
    {
        var deliverableId1 = DeliverableId.FromString(one);
        var deliverableId2 = DeliverableId.FromString(two);

        (deliverableId1 == deliverableId2).Should().Be(expected);
    }
}

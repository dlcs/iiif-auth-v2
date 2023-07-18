using IIIFAuth2.API.Models;

namespace IIIFAuth2.API.Tests.Models;

public class AssetIdTests
{
    [Fact]
    public void ToString_CorrectFormat()
    {
        var assetImageId = new AssetId(19, 4, "my-first-image");
        const string expected = "19/4/my-first-image";

        assetImageId.ToString().Should().Be(expected);
    }
    
    [Fact]
    public void FromString_ReturnsExpected()
    {
        const string assetId = "19/4/my-first-image";

        var assetImageId = AssetId.FromString(assetId);

        assetImageId.Customer.Should().Be(19);
        assetImageId.Space.Should().Be(4);
        assetImageId.Asset.Should().Be("my-first-image");
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("foo/bar")]
    [InlineData("foo/bar/baz")]
    public void FromString_Throws_IfInvalidFormat(string candidate)
    {
        Action action = () => AssetId.FromString(candidate);
        action.Should()
            .Throw<FormatException>()
            .WithMessage($"AssetId '{candidate}' is invalid. Must be in format customer/space/asset");
    }

    [Fact]
    public void CanDeconstruct()
    {
        var assetId = new AssetId(99, 100, "same");
        var (customer, space, asset) = assetId;

        customer.Should().Be(99);
        space.Should().Be(100);
        asset.Should().Be("same");
    }

    [Fact]
    public void Equals_Compares_Values()
    {
        var assetId1 = new AssetId(99, 100, "same");
        var assetId2 = new AssetId(99, 100, "same");

        assetId1.Equals(assetId2).Should().BeTrue();
    }
    
    [Theory]
    [InlineData("99/100/foo", "99/100/foo", true)]
    [InlineData("99/100/foo", "89/100/foo", false)]
    [InlineData("99/100/foo", "99/1/foo", false)]
    [InlineData("99/100/foo", "99/100/bar", false)]
    public void EqualsOperator_Compares_Values(string one, string two, bool expected)
    {
        var assetId1 = AssetId.FromString(one);
        var assetId2 = AssetId.FromString(two);

        (assetId1 == assetId2).Should().Be(expected);
    }

    [Fact]
    public void EqualsOperator_HandlesNull()
    {
        var assetId = new AssetId(1, 2, "foo");
        AssetId? nullAsset = null;

        (assetId == nullAsset).Should().BeFalse();
        (nullAsset == assetId).Should().BeFalse();
        (nullAsset == nullAsset).Should().BeTrue();
    }
    
    [Theory]
    [InlineData("99/100/foo", "99/100/foo", false)]
    [InlineData("99/100/foo", "89/100/foo", true)]
    [InlineData("99/100/foo", "99/1/foo", true)]
    [InlineData("99/100/foo", "99/100/bar", true)]
    public void NotEqualsOperator_Compares_Values(string one, string two, bool expected)
    {
        var assetId1 = AssetId.FromString(one);
        var assetId2 = AssetId.FromString(two);

        (assetId1 != assetId2).Should().Be(expected);
    }
    
    [Fact]
    public void NotEqualsOperator_HandlesNull()
    {
        var assetId = new AssetId(1, 2, "foo");
        AssetId? nullAsset = null;

        (assetId != nullAsset).Should().BeTrue();
        (nullAsset != assetId).Should().BeTrue();
        (nullAsset != nullAsset).Should().BeFalse();
    }
}
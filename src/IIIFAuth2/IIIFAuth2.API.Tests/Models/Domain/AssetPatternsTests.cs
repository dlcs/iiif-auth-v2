using IIIFAuth2.Core.Models.Domain;

namespace IIIFAuth2.API.Tests.Models.Domain;

public class AssetPatternsTests
{
    private const int Customer = 7;
    private const int Space = 1;

    private static readonly AssetMetadata FullAsset = new()
    {
        Scopes = ["manifest-a", "manifest-b"],
        Tags = ["tag-a"],
        String1 = "LIDDLE/WW2/HFIU/001",
        String2 = "foo",
        String3 = "bar",
        Number1 = 10,
        Number2 = 20,
        Number3 = 30,
    };

    [Fact]
    public void Matches_True_IfNoPatterns()
    {
        new AssetPatterns().Matches(Customer, Space, FullAsset).Should().BeTrue();
        new AssetPatterns().Matches(Customer, Space, null).Should().BeTrue();
    }

    [Fact]
    public void Matches_True_IfAllFieldsEmptyArrays()
    {
        var sut = new AssetPatterns
        {
            Customer = [], Space = [], Scopes = [], Tags = [], String1 = [], String2 = [], String3 = [],
            Number1 = [], Number2 = [], Number3 = []
        };

        sut.Matches(Customer, Space, null).Should().BeTrue();
    }

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public void Matches_Customer(int customer, bool expected)
        => new AssetPatterns { Customer = [7, 9] }.Matches(customer, Space, null).Should().Be(expected);

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Matches_Space(int space, bool expected)
        => new AssetPatterns { Space = [1, 3] }.Matches(Customer, space, null).Should().Be(expected);

    [Theory]
    [InlineData(new[] { "manifest-b" }, true)]
    [InlineData(new[] { "manifest-x", "manifest-a" }, true)]
    [InlineData(new[] { "manifest-*" }, false)]
    [InlineData(new[] { "manifest-x" }, false)]
    [InlineData(new[] { "MANIFEST-A" }, false)]
    public void Matches_Scopes_AnyOf(string[] pattern, bool expected)
        => new AssetPatterns { Scopes = pattern }.Matches(Customer, Space, FullAsset).Should().Be(expected);

    [Theory]
    [InlineData(new[] { "tag-a" }, true)]
    [InlineData(new[] { "tag*" }, false)]
    [InlineData(new[] { "tag-b" }, false)]
    public void Matches_Tags_AnyOf(string[] pattern, bool expected)
        => new AssetPatterns { Tags = pattern }.Matches(Customer, Space, FullAsset).Should().Be(expected);

    [Theory]
    [InlineData("Liddle", "Liddle", true)]
    [InlineData("Liddle", "Liddles", false)]
    [InlineData("Liddle", "Liddle/", false)]
    [InlineData("Liddle/", "Liddle/", true)]
    [InlineData("Liddle/", "Liddle/ww2", false)]
    [InlineData("Liddle/*", "Liddle/ww2", true)]
    [InlineData("Liddle/*", "Liddle/", true)]
    [InlineData("Liddle/*", "Liddle", false)]
    [InlineData("Liddle/*", "Liddles", false)]
    [InlineData("Liddle*", "Liddle", true)]
    [InlineData("Liddles*", "Liddle", false)]
    [InlineData("Liddle*", "Liddles", true)]
    [InlineData("Liddle*", "Liddle/ww2", true)]
    [InlineData("Li**le", "Li**le", true)]
    [InlineData("Li**le", "Liddle", false)]
    [InlineData("Li*", "Liddle", true)]
    [InlineData("Li*le", "Liddle", false)]
    [InlineData("Li**le*", "Li**leSomethingElse", true)]
    [InlineData("Li**le*", "LiddleSomethingElse", false)]
    [InlineData("*", "anything", true)]
    [InlineData("liddle*", "Liddle", false)]
    public void Matches_String_ExactOrTrailingWildcard(string pattern, string value, bool expected)
    {
        var asset = new AssetMetadata { String1 = value, String2 = value, String3 = value };

        new AssetPatterns { String1 = [pattern] }.Matches(Customer, Space, asset).Should().Be(expected);
        new AssetPatterns { String2 = [pattern] }.Matches(Customer, Space, asset).Should().Be(expected);
        new AssetPatterns { String3 = [pattern] }.Matches(Customer, Space, asset).Should().Be(expected);
    }

    [Fact]
    public void Matches_String_AnyOf()
        => new AssetPatterns { String1 = ["OTHER", "LIDDLE/WW2/*"] }.Matches(Customer, Space, FullAsset)
            .Should().BeTrue();

    [Fact]
    public void Matches_Numbers()
    {
        new AssetPatterns { Number1 = [10], Number2 = [1, 20], Number3 = [30] }.Matches(Customer, Space, FullAsset)
            .Should().BeTrue();
        new AssetPatterns { Number1 = [11] }.Matches(Customer, Space, FullAsset).Should().BeFalse();
        new AssetPatterns { Number2 = [10] }.Matches(Customer, Space, FullAsset).Should().BeFalse();
        new AssetPatterns { Number3 = [20] }.Matches(Customer, Space, FullAsset).Should().BeFalse();
    }

    [Fact]
    public void Matches_False_IfAnyFieldDoesNotMatch()
        => new AssetPatterns { Space = [1], Tags = ["tag-a"], String1 = ["OTHER"] }
            .Matches(Customer, Space, FullAsset).Should().BeFalse();

    [Fact]
    public void Matches_True_IfAllFieldsMatch()
        => new AssetPatterns
            {
                Customer = [Customer], Space = [Space], Scopes = ["manifest-a"], Tags = ["tag-a"],
                String1 = ["LIDDLE/*"], String2 = ["foo"], String3 = ["bar"], Number1 = [10], Number2 = [20],
                Number3 = [30]
            }
            .Matches(Customer, Space, FullAsset).Should().BeTrue();

    public static TheoryData<AssetPatterns> MetadataConstrainedPatterns => new()
    {
        new AssetPatterns { Scopes = ["manifest-a"] },
        new AssetPatterns { Tags = ["tag-a"] },
        new AssetPatterns { String1 = ["*"] },
        new AssetPatterns { String2 = ["foo"] },
        new AssetPatterns { String3 = ["bar"] },
        new AssetPatterns { Number1 = [10] },
        new AssetPatterns { Number2 = [20] },
        new AssetPatterns { Number3 = [30] },
    };

    [Theory]
    [MemberData(nameof(MetadataConstrainedPatterns))]
    public void Matches_False_IfFieldConstrained_AndAssetNull(AssetPatterns sut)
        => sut.Matches(Customer, Space, null).Should().BeFalse();

    [Theory]
    [MemberData(nameof(MetadataConstrainedPatterns))]
    public void Matches_False_IfFieldConstrained_AndAssetValueMissing(AssetPatterns sut)
        => sut.Matches(Customer, Space, new AssetMetadata()).Should().BeFalse();

    [Fact]
    public void Matches_CustomerAndSpaceOnly_True_IfAssetNull()
        => new AssetPatterns { Customer = [Customer], Space = [Space] }.Matches(Customer, Space, null)
            .Should().BeTrue();
}

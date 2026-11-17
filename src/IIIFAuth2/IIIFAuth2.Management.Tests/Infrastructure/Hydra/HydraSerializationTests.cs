using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IIIFAuth2.Management.Infrastructure.Hydra;

namespace IIIFAuth2.Management.Tests.Infrastructure.Hydra;

public class HydraSerializationTests
{
    // Matches Program.cs: web defaults (camelCase) + ignore nulls
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    [Fact]
    public void Error_HasHydraAndProblemFields_JsonLdFirst()
    {
        var error = (Error)Error.ToResult(404, "Not Found", "missing", "http://test/x").Value!;

        var json = JsonSerializer.Serialize(error, Options);

        json.Should().StartWith("""{"@type":"Error",""");
        var node = JsonNode.Parse(json)!;
        node["statusCode"]!.GetValue<int>().Should().Be(404);
        node["status"]!.GetValue<int>().Should().Be(404);
        node["description"]!.GetValue<string>().Should().Be("missing");
        node["detail"]!.GetValue<string>().Should().Be("missing");
        node["instance"]!.GetValue<string>().Should().Be("http://test/x");
        node["@context"].Should().BeNull();
    }

    [Fact]
    public void AddPaging_NoView_IfSinglePage()
    {
        var collection = new HydraCollection<int> { Id = "http://test/c", Members = [1, 2], TotalItems = 2 };

        PartialCollectionView.AddPaging(collection, 1, 50);

        collection.View.Should().BeNull();
    }

    [Fact]
    public void AddPaging_MiddlePage_HasAllLinks_WithEncodedFurtherParams()
    {
        var collection = new HydraCollection<int>
            { Id = "http://test/c?page=2&name=a b", Members = [1, 2], TotalItems = 5 };

        PartialCollectionView.AddPaging(collection, 2, 2, [new("name", "a b")]);

        var view = collection.View!;
        view.TotalPages.Should().Be(3);
        view.Id.Should().Be("http://test/c?page=2&pageSize=2&name=a%20b");
        view.First.Should().Be("http://test/c?page=1&pageSize=2&name=a%20b");
        view.Previous.Should().Be("http://test/c?page=1&pageSize=2&name=a%20b");
        view.Next.Should().Be("http://test/c?page=3&pageSize=2&name=a%20b");
        view.Last.Should().Be("http://test/c?page=3&pageSize=2&name=a%20b");
    }

    [Fact]
    public void AddPaging_LastPage_NoNextOrLast()
    {
        var collection = new HydraCollection<int> { Id = "http://test/c", Members = [5], TotalItems = 5 };

        PartialCollectionView.AddPaging(collection, 3, 2);

        collection.View!.Next.Should().BeNull();
        collection.View.Last.Should().BeNull();
        collection.View.Previous.Should().Be("http://test/c?page=2&pageSize=2");
    }
}

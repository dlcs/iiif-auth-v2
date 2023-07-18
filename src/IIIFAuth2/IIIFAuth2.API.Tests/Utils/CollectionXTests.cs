using IIIFAuth2.API.Utils;

namespace IIIFAuth2.API.Tests.Utils;

public class CollectionXTests
{
    [Fact]
    public void IsNullOrEmpty_List_True_IfNull()
    {
        List<int>? coll = null;

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_List_True_IfEmpty()
    {
        var coll = new List<int>();

        coll.IsNullOrEmpty().Should().BeTrue();
    }
    
    [Fact]
    public void IsNullOrEmpty_List_False_IfHasValues()
    {
        var coll = new List<int> {2};

        coll.IsNullOrEmpty().Should().BeFalse();
    }
}
using IIIFAuth2.Tests.Common;

namespace IIIFAuth2.Management.Tests.TestingInfrastructure;

[CollectionDefinition(CollectionName)]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
    public const string CollectionName = "Database Collection";
}

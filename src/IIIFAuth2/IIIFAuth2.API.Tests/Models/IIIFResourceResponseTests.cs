using IIIF.Auth.V2;
using IIIFAuth2.API.Models;
using IIIFAuth2.API.Models.Result;

namespace IIIFAuth2.API.Tests.Models;

public class IIIFResourceResponseTests
{
    [Fact]
    public void Failure_SetsErrorAndErrorMessage()
    {
        var response = IIIFResourceResponse.Failure("uhoh");

        response.EntityNotFound.Should().BeFalse();
        response.Error.Should().BeTrue();
        response.ErrorMessage.Should().Be("uhoh");
        response.DescriptionResource.Should().BeNull();
    }
    
    [Fact]
    public void NotFound_SetsEntityNotFoundAndErrorMessage()
    {
        var response = IIIFResourceResponse.NotFound("uhoh");

        response.EntityNotFound.Should().BeTrue();
        response.Error.Should().BeFalse();
        response.ErrorMessage.Should().Be("uhoh");
        response.DescriptionResource.Should().BeNull();
    }
    
    [Fact]
    public void Success_SetsDescriptionResource()
    {
        var authAccessToken2 = new AuthAccessToken2();
        var response = IIIFResourceResponse.Success(authAccessToken2);

        response.EntityNotFound.Should().BeFalse();
        response.Error.Should().BeFalse();
        response.ErrorMessage.Should().BeNull();
        response.DescriptionResource.Should().Be(authAccessToken2);
    }
}
﻿using IIIFAuth2.API.Utils;
using Microsoft.AspNetCore.Http;

namespace IIIFAuth2.API.Tests.Utils;

public class HttpRequestXTests
{
    [Fact]
    public void GetDisplayUrl_ReturnsFullUrl_WhenCalledWithDefaultParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_WithPathBase_ReturnsFullUrl_WhenCalledWithDefaultParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl();
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_ReturnsFullUrl_WhenCalledWithPath()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/my-path/one/two?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl("/my-path/one/two");
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_WithPathBase_ReturnsFullUrl_WhenCalledWithWithPath()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2/my-path/one/two?foo=bar";

        // Act
        var result = httpRequest.GetDisplayUrl("/my-path/one/two");
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_ReturnsFullUrl_WithoutQueryParam_WhenCalledWithDoNotIncludeQueryParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example";

        // Act
        var result = httpRequest.GetDisplayUrl(includeQueryParams: false);
        
        // Assert
        result.Should().Be(expected);
    }
    
    [Fact]
    public void GetDisplayUrl_WithPathBase_ReturnsFullUrl_WithoutQueryParam_WhenCalledWithDoNotIncludeQueryParams()
    {
        // Arrange
        var httpRequest = new DefaultHttpContext().Request;
        httpRequest.Path = new PathString("/anything");
        httpRequest.QueryString = new QueryString("?foo=bar");
        httpRequest.Host = new HostString("test.example");
        httpRequest.PathBase = new PathString("/v2");
        httpRequest.Scheme = "https";

        const string expected = "https://test.example/v2";

        // Act
        var result = httpRequest.GetDisplayUrl(includeQueryParams: false);
        
        // Assert
        result.Should().Be(expected);
    }
}
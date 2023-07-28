namespace IIIFAuth2.API.Infrastructure.Auth.Models;

/// <summary>
/// View model for rendering the SignificantGesture view. A user is required to carry out a 'significant gesture' in a
/// domain for a cookie to be issued, this captures a confirmation click to allow DLCS to issue a token that the browser
/// will honour 
/// </summary>
/// <param name="SignificantGestureTitle">Title of page</param>
/// <param name="SignificantGestureMessage">Information message to display to user</param>
/// <param name="SingleUseToken">
/// Single use correlation id, from this we can lookup CustomerId, AccessServiceName + Roles to grant to user.
/// </param>
public record SignificantGestureModel(
    string SignificantGestureTitle, 
    string SignificantGestureMessage,
    string SingleUseToken);
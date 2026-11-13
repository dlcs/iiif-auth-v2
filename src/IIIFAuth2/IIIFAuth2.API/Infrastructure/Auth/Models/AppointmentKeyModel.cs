namespace IIIFAuth2.API.Infrastructure.Auth.Models;

/// <summary>
/// View model for rendering the AppointmentKey view, where the user enters their appointment key. Submitting the form
/// is the significant gesture, so a cookie can be issued in response.
/// </summary>
/// <param name="PostbackUri">Uri to post the form to (the access service)</param>
/// <param name="Title">Title of page</param>
/// <param name="Message">Information message to display to user</param>
/// <param name="Origin">Origin of the requesting client, posted back with the key</param>
/// <param name="Error">Optional error message, e.g. if previously entered key was invalid</param>
public record AppointmentKeyModel(
    Uri PostbackUri,
    string Title,
    string Message,
    string Origin,
    string? Error = null);

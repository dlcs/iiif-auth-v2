using IIIF.Auth.V2;
using IIIF.Presentation.V3.Strings;

namespace IIIFAuth2.API.Features.Access;

internal static class AuthServiceBuilder
{
    public static AuthAccessTokenError2 CreateAuthAccessTokenError2(string profile, string messageId, string heading,
        string note)
        => new(profile, new LanguageMap("en", note))
        {
            Heading = new LanguageMap("en", heading),
            MessageId = messageId,
        };
}
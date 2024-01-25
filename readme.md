# IIIF Auth 2

IIIF Auth v2 handles [IIIF Authorization Flow API 2.0](https://iiif.io/api/auth/2.0/) requests and is an implmementation of [DLCS RFC 012](https://github.com/dlcs/protagonist/blob/31bcd7db4d4856620b44b03e63d91d11e6832c62/docs/rfcs/012-auth-service.md)

## Running

There is a Dockerfile and docker-compose file for running app:

```bash
# build docker image
docker build -t iiif-auth-2:local .  

# run image
docker run -it --rm \
    --name iiif-auth-2 \
    -p "8014:80" \
    iiif-auth-2:local

# run via docker-compose
docker compose up
```

For local debugging the `docker-compose.local.yml` file can be used, this will start an empty Postgres instance.

```bash
docker compose -f docker-compose.local.yml up
```

## Configuration

The following appSetting configuration values are supported:

| Name                               | Description                                                                                                           | Default                                         |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------- |
| OrchestratorRoot                   | Base URI for Orchestrator, used to generate links                                                                     |                                                 |
| DefaultSignificantGestureTitle     | Fallback title to use on SignificantGesture.cshtml                                                                    | `"Click to continue"`                           |
| DefaultSignificantGestureMessage   | Fallback message to use on SignificantGesture.cshtml                                                                  | `"You will now be redirected to DLCS to login"` |
| Auth__CookieNameFormat             | Name of issued cookie, `{0}` value replaced with customer Id                                                          | `"dlcs-auth2-{0}`                               |
| Auth__SessionTtl                   | Default TTL for sessions + cookies (in seconds)                                                                       | 600                                             |
| Auth__RefreshThreshold             | UserSession expiry not refreshed if LastChecked within this number of secs                                            | 120                                             |
| Auth__JwksTtl                      | How long to cache results of JWKS calls for, in secs                                                                  | 600                                             |
| GesturePathTemplateForDomain       | Dictionary that allows control of domain-specific significant gesture paths. `{customerId}` replaced.                 |                                                 |
| OAuthCallbackPathTemplateForDomain | Dictionary that allows control of domain-specific oauth2 callback paths. `{customerId}` + `{accessService}` replaced. |                                                 |

> A note on Dictionarys for domain-specific paths. A key of `"Default"` serves as fallback but isn't necessary if the default value matches the canonical DLCS path.

## Migrations

Migrations can be added by running the following:

```bash
cd src/IIIFAuth2

dotnet ef migrations add "{migration-name}" -p IIIFAuth2.API -o Data/Migrations
```

Migrations are applied on startup, regardless of environment, if `"RunMigrations" = "true"`.

## Local Development

This service is an extension of [DLCS Protagonist](https://github.com/dlcs/protagonist/) and once deployed will run under the same host as the main DLCS.

Below are steps for running iiif-auth-v2 and Orchestrator locally:

1. Create a `customer_cookie_domain` for required customer (e.g. 99). As orchestrator and iiif-auth-v2 will be running on different ports this is necessary as otherwise orchestrator won't see required cookies.
   * `INSERT INTO customer_cookie_domains (customer, domains) values (99, 'localhost');`
2. In iiif-auth-v2 set `"OrchestratorRoot": "https://localhost:5003"` appSetting (default from orchestrator launchSettings)
3. In orchestrator set `"Auth__Auth2ServiceRoot": "https://localhost:7049/auth/v2/"` appSetting (default from orchestrator launchSettings)
4. In orchestrator change the last line of `ConfigDrivenAuthPathGenerator.GetAuth2PathForRequest` to:
```cs
var auth2PathForRequest = request.GetDisplayUrl(path, includeQueryParams: false);
return auth2PathForRequest.Contains("probe") ? auth2PathForRequest : auth2PathForRequest.Replace(host, "localhost:7049");
```

> Point 4 is a hack and should be addressed in a better manner. It is required as the default path rewrite rules won't work as all auth paths aren't on the root domain
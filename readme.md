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

| Name                             | Description                                                  | Default                                         |
| -------------------------------- | ------------------------------------------------------------ | ----------------------------------------------- |
| OrchestratorRoot                 | Base URI for Orchestrator, used to generate links            |                                                 |
| DefaultSignificantGestureTitle   | Fallback title to use on SignificantGesture.cshtml           | `"Click to continue"`                           |
| DefaultSignificantGestureMessage | Fallback message to use on SignificantGesture.cshtml         | `"You will now be redirected to DLCS to login"` |
| Auth__CookieNameFormat           | Name of issued cookie, `{0}` value replaced with customer Id | `"dlcs-auth2-{0}`                               |
| Auth__SessionTtl                 | Default TTL for sessions + cookies (in seconds)              | 600                                             |
| Auth__CookieDomains              | An optional list of domains to issue cookies for             |                                                 |
| Auth__UseCurrentDomainForCookie  | Whether current domain is automatically added to auth token  | `true`                                          |


## Migrations

Migrations can be added by running the following:

```bash
cd src/IIIFAuth2

dotnet ef migrations add "{migration-name}" -p IIIFAuth2.API -o Data/Migrations
```

Migrations are applied on startup, regardless of environment, if `"RunMigrations" = "true"`.
# IIIF Auth 2

IIIF Auth v2 handles [IIIF Authorization Flow API 2.0](https://iiif.io/api/auth/2.0/) requests and is an implmementation of [DLCS RFC 012](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/012-auth-service.md)

## Role Provider Types

The following role provider types are supported, this will be extended over time:

* `clickthrough` - auth service will render agreement, on accepting the user is granted specified roles. Not external dependencies.
* `oidc` - external authorization server is used for login, claims are mapped to DLCS roles, see [DLCS RFC 008](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/008-more-access-control-oidc-oauth.md)

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

For local debugging there are 3 docker compose files available:
* `docker-compose.db.yml` - runs an empty postgres instance. Running sln with `RunMigrations=true` will scaffold DB.
* `docker-compose.local.yml` - runs the above and also an nginx container, which is:
  * Running on `https://localhost:5040`.
  * Proxying `/auth/v2/probe/*` and `/*` to localhost:5013. This is the http port for Orchestrator as defined in `launchSettings.json`
  * Proxying `/auth/v2/*` to localhost:7149. This is the http port for iiif-auth-v2, as defined in `launchSettings.json`
* `docker-compose.local.certs.yml` - same as `docker-compose.local.yml` but nginx uses your local .NET dev certificate rather than a self-signed one, avoiding browser errors. See [Using .NET dev certificates](#using-net-dev-certificates).

```bash
# run postgres DB only
$ docker compose -f docker-compose.db.yml up

# run postgres DB and nginx proxy
$ docker compose -f docker-compose.local.yml up

# run postgres DB and nginx proxy, using .NET dev certificates
$ docker compose -f docker-compose.local.certs.yml up
```

> [!WARNING]
> The nginx container in `docker-compose.local.yml` uses a self-signed cert. This will show browser errors but is enough for local testing.

### Using .NET dev certificates

`docker-compose.local.certs.yml` mounts `nginx/certs/` into the nginx container at `/etc/nginx/ssl`, and expects `nginx/certs/cert.pem` and `nginx/certs/key.pem` to exist, these need to be exported from your trusted dotnet dev certificate before running:

```bash
# ensure a trusted dev certificate exists
$ dotnet dev-certs https --trust

# export as PEM - this writes nginx/certs/cert.pem and nginx/certs/cert.key
$ dotnet dev-certs https --export-path nginx/certs/cert.pem --format Pem --no-password

# rename key to the name expected by DockerfileLocalCerts
$ mv nginx/certs/cert.key nginx/certs/key.pem
```

> [!NOTE]
> The private key is exported unencrypted, don't share it. 
> 
> The certificate is mounted at runtime, if the dev certificate is regenerated, re-export and restart the nginx container.

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
| RunMigrations                      | If true, EF migrations will be run when app runs                                                                      | `false`                                         |

> A note on Dictionarys for domain-specific paths. A key of `"Default"` serves as fallback but isn't necessary if the default value matches the canonical DLCS path.

## Migrations

Migrations can be added by running the following:

```bash
cd src/IIIFAuth2

dotnet ef migrations add "{migration-name}" -p IIIFAuth2.API -o Data/Migrations
```

Migrations are applied on startup, regardless of environment, if `"RunMigrations" = "true"`.

## Local Development

This service is an extension of [DLCS Protagonist](https://github.com/dlcs/protagonist/) and when deployed will run under the same host as the main DLCS, with routing rules controlled at load-balancer level.

Below are steps for running iiif-auth-v2 and Orchestrator locally:

1. Run `docker compose -f docker-compose.local.yml up`
2. In iiif-auth-v2 set `"OrchestratorRoot": "https://localhost:5040"` appSetting (nginx port)
3. In orchestrator set `"Auth__Auth2ServiceRoot": "https://localhost:7049/auth/v2/"` appSetting (default from auth-services launchSettings)
4. When making Orchestrator requests use the nginx port `:5040`, rather than the standard dotnet launchSettings port. This ensures that auth + orchestrator are on same port and avoids any cookie issues.

### One Off

You'll need to setup an access service and role provider locally, see [bootstrap.sql](./scripts/sql/bootstrap.sql) for example of setting up clickthrough provider.

### Testing

There's a sample auth client available at https://tomcrane.github.io/iiif-auth-client/ that is useful for testing. This shows verbose logging to help diagnose and track any issues.

* https://tomcrane.github.io/iiif-auth-client/?image=https://localhost:5040/iiif-img/{asset-id}/info.json
* https://tomcrane.github.io/iiif-auth-client/?manifest=https://localhost:5040/iiif-manifest/{asset-id}

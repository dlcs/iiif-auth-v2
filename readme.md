# IIIF Auth 2

IIIF Auth v2 handles [IIIF Authorization Flow API 2.0](https://iiif.io/api/auth/2.0/) requests and is an implmementation of [DLCS RFC 012](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/012-auth-service.md)

## Role Provider Types

The following role provider types are supported, this will be extended over time:

* `clickthrough` - auth service will render agreement, on accepting the user is granted specified roles. Not external dependencies.
* `oidc` - external authorization server is used for login, claims are mapped to DLCS roles, see [DLCS RFC 008](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/008-more-access-control-oidc-oauth.md)

## Projects

The solution contains the following projects:

* `IIIFAuth2.API` - public API, handles IIIF Auth 2 requests (probe, access, token etc) and verify-access requests.
* `IIIFAuth2.Management` - internal management API for updating auth configuration. Owns DB migrations.
* `IIIFAuth2.Core` - shared EF context, entities, migrations and domain models.
* `IIIFAuth2.Tests.Common` - shared test infrastructure (e.g. Postgres `DatabaseFixture`).

## Running

There are Dockerfiles for each app and a docker-compose file for running both:

```bash
# build docker images
docker build -t iiif-auth-2:local .  
docker build -t iiif-auth-2-management:local -f Dockerfile.Management .

# run image
docker run -it --rm \
    --name iiif-auth-2 \
    -p "8014:80" \
    iiif-auth-2:local

# run via docker-compose - management runs migrations and is available on port 8015
docker compose up
```

For local debugging there are 3 docker compose files available:
* `docker-compose.db.yml` - runs an empty postgres instance. Running `IIIFAuth2.Management` with `RunMigrations=true` will scaffold DB.
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

> A note on Dictionarys for domain-specific paths. A key of `"Default"` serves as fallback but isn't necessary if the default value matches the canonical DLCS path.

### Management

The following appSetting configuration values are supported by `IIIFAuth2.Management`:

| Name                     | Description                                                                 | Default |
| ------------------------ | --------------------------------------------------------------------------- | ------- |
| Dlcs__ApiRoot            | Base URI for DLCS API, used to validate incoming credentials                |         |
| Dlcs__ApiDefaultTimeoutMs | Timeout for requests to DLCS API, in ms                                    | 30000   |
| AuthCacheTtl             | How long to cache successful credential checks for, in secs                 | 60      |
| RunMigrations            | If true, EF migrations will be run when app runs                            | `false` |

All management routes are in the format `/customers/{customerId}/...`. Requests are authenticated by forwarding the provided `Authorization` header to DLCS API `/customers/{customerId}` - a 200 response means the request is authenticated.

## Migrations

Migrations can be added by running the following:

```bash
cd src/IIIFAuth2

dotnet ef migrations add "{migration-name}" -p IIIFAuth2.Core -s IIIFAuth2.Management -o Data/Migrations
```

Migrations are applied on `IIIFAuth2.Management` startup, regardless of environment, if `"RunMigrations" = "true"`. `IIIFAuth2.API` does not run migrations.

## Local Development

This service is an extension of [DLCS Protagonist](https://github.com/dlcs/protagonist/) and when deployed will run under the same host as the main DLCS, with routing rules controlled at load-balancer level.

Below are steps for running iiif-auth-v2 and Orchestrator locally:

1. Run `docker compose -f docker-compose.local.yml up`
2. In iiif-auth-v2 set `"OrchestratorRoot": "https://localhost:5040"` appSetting (nginx port)
3. In orchestrator set `"Auth__Auth2ServiceRoot": "https://localhost:7049/auth/v2/"` appSetting (default from auth-services launchSettings)
4. When making Orchestrator requests use the nginx port `:5040`, rather than the standard dotnet launchSettings port. This ensures that auth + orchestrator are on same port and avoids any cookie issues.

### One Off

You'll need to setup an access service and role provider locally, see [bootstrap.sql](./scripts/sql/bootstrap.sql) for example of setting up clickthrough provider.

### Test UI

`IIIFAuth2.TestUI` is a Razor Pages app that replaces Orchestrator + a IIIF client for local testing. Run it alongside IIIFAuth2.API (`https://localhost:7050`, no nginx or Orchestrator required):

* **Setup** (`/`) - register a deliverable id (`1/2/foo` or `99/100/page1.jpg/mets.xml`) and its roles. Roles are read from the `roles` table, or can be free-typed to simulate unknown roles. Deliverables are held in memory.
* **Auth flow** (`/Flow?id=...`) - a browser client that loads a fake manifest and runs probe -> access service -> token service -> probe -> content, logging each step along with downstream calls to iiif-auth-v2.
* **Downstream calls** (`/Calls`) - every request the fake Orchestrator made to iiif-auth-v2, with headers and bodies.

Fake Orchestrator endpoints:
* `/manifest/{deliverableId}` - single-canvas manifest, auth services from `/services/` endpoint.
* `/auth/v2/probe/{deliverableId}` - calls `/probe_internal/`, passing bearer token. Set iiif-auth-v2 `"OrchestratorRoot": "https://localhost:7050"` to have probe service ids point here, or leave the "send probe requests to this Test UI" option ticked.
* `/fake-image/{deliverableId}` - calls `/verifyaccess/`, forwarding the request `Cookie` header, returns an SVG showing result.

Configured via `AuthServiceRoot` (default `https://localhost:7049/auth/v2/`) and `ConnectionStrings:Postgres` appSettings. The test UI needs to run on https on the same host as iiif-auth-v2 so the browser sends the auth cookie to the fake image endpoint (cookies ignore ports).

### Testing

There's a sample auth client available at https://tomcrane.github.io/iiif-auth-client/ that is useful for testing. This shows verbose logging to help diagnose and track any issues.

* https://tomcrane.github.io/iiif-auth-client/?image=https://localhost:5040/iiif-img/{asset-id}/info.json
* https://tomcrane.github.io/iiif-auth-client/?manifest=https://localhost:5040/iiif-manifest/{asset-id}

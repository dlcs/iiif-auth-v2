# IIIF Auth 2

IIIF Auth v2 handles [IIIF Authorization Flow API 2.0](https://iiif.io/api/auth/2.0/) requests and is an implmementation of [DLCS RFC 012](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/012-auth-service.md)

## Role Provider Types

The following role provider types are supported, this will be extended over time:

* `clickthrough` - auth service will render agreement, on accepting the user is granted specified roles. Not external dependencies.
* `oidc` - external authorization server is used for login, claims are mapped to DLCS roles, see [DLCS RFC 008](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/008-more-access-control-oidc-oauth.md)
* `ipRestriction` - roles are granted when the client IP address is within configured CIDR ranges (e.g. a reading room). No sign-in is required and roles are never stored in a session, they are evaluated on each request. By convention, roles granted by an IP restriction have a final path segment prefixed `ip:` (e.g. `https://api.dlcs.io/customers/2/roles/ip:reading-room`). Configuration:

```json
{
  "default": {
    "config": "IpRestriction",
    "ranges": [ "10.0.0.0/24", "2001:db8:1007::/48" ],
    "locationPhrase": "in the Research Centre"
  }
}
```

`locationPhrase` is optional and is public-facing. It includes the preposition, as this varies by location, e.g. "in the Reading Room", "on Main Campus", "via the company VPN". It is output:

* in probe responses, as "Only available {locationPhrase}" (e.g. "Only available in the Research Centre"). If an asset has several IP roles, all their phrases are listed (joined with "or").
* as the default label of the `external` access service: "Available {locationPhrase}".
* as the default `errorNote` of the `external` access service's token service, with `errorHeading` "Not available at your location". Clients use the token service immediately for `external` services, and it fails if the client isn't at a permitted location. These defaults only apply if `access_token_error_heading` isn't set on the access service.

Omit it if the location shouldn't be advertised, and generic wording ("Only available from permitted locations") will be used. Generic wording is also used in probe responses if *any* of the asset's IP role providers has no `locationPhrase`, as listing only some locations would be incomplete. Setting `UseGenericLocationWording` uses generic wording everywhere, for all providers. IP ranges are never output.

* `appointment` - the user enters an appointment key in the access service window. Each appointment grants its roles, between its `start` and `end`, for assets matching its asset patterns (space, scopes, tags, string1-3, number1-3). The key form submission is the significant gesture; if the user already has a session for the customer the appointment is added to it, otherwise a new session is created. Appointment roles are never stored in the session, they are evaluated on each request. By convention, roles granted by an appointment have a final path segment prefixed `appointment:` (e.g. `https://api.dlcs.io/customers/2/roles/appointment:token-based`). Appointments are currently created via SQL (see `scripts/sql/appointment.sql`). Configuration:

```json
{
  "default": {
    "config": "Appointment",
    "gestureTitle": "Use your appointment key",
    "gestureMessage": "Enter the key from your appointment confirmation"
  }
}
```

`gestureTitle` and `gestureMessage` are optional and are used as the title and message of the key form, falling back to the default significant gesture wording.

IpRestriction providers should only have a `default` configuration. Host-specific keys are looked up using the request host, but IP roles are evaluated on `probe_internal` and `verifyaccess`, which Orchestrator calls internally. So the host is the one Orchestrator uses to call this service, not the public host, unless Orchestrator forwards `x-forwarded-host`. The `external` access service's default label and token service errors always use `default`.

Roles granted by an IpRestriction provider should belong to an access service with `external` profile. These are output first in services descriptions, without a logout service. They have an `id`, which the spec says "should not" be present and clients must ignore, so the service can be referenced, e.g. declared once in a Manifest and referenced by id. Opening it returns a "not interactive" error.

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
  * Running on `https://localhost:5041`.
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
| KnownNetworks                      | Comma separated CIDR ranges of proxies that `x-forwarded-*` headers are accepted from. See note below.                | `"AllNetworks"`                                 |
| ForwardLimit                       | Maximum number of entries in `x-forwarded-*` headers that are processed (rightmost first). See note below.            | 2                                               |
| UseGenericLocationWording          | If `true`, IpRestriction wording always uses generic "permitted locations" text, ignoring `locationPhrase`            | `false`                                         |

> A note on Dictionarys for domain-specific paths. A key of `"Default"` serves as fallback but isn't necessary if the default value matches the canonical DLCS path.

> A note on `KnownNetworks` and `ForwardLimit`. The client IP grants `IpRestriction` roles, and is read from `x-forwarded-for`. This service is called by Orchestrator rather than by clients, so Orchestrator is expected to work out the client IP and send it as the only `x-forwarded-for` value.
>
> * `KnownNetworks` lists the proxies that `x-forwarded-*` headers are accepted *from*. It is checked for every hop, not just the direct caller. Starting from the direct caller, the next `x-forwarded-for` entry (from the right) is applied only if the current address is known, up to `ForwardLimit` entries. The last address applied is the client IP.
> * In deployed environments, set it to the network(s) of the load balancer and Orchestrator. If Orchestrator calls via a load balancer that sees it by a public IP (e.g. through a NAT gateway), include that IP too.
> * Never add client location ranges (e.g. a reading room). An address in `KnownNetworks` is trusted to say who is behind it, so a client in that range could claim any IP.
> * The default `ForwardLimit` of `2` handles Orchestrator calling via a load balancer (`x-forwarded-for: client, orchestrator`) and calling directly (`x-forwarded-for: client`).
> * The client IP is unknown, and no `IpRestriction` roles are granted, if:
>   * `x-forwarded-for` is missing or wasn't accepted. The direct caller's IP is never used as the client IP.
>   * the resolved IP is itself within `KnownNetworks`, i.e. it's a proxy. E.g. Orchestrator called via a load balancer without sending `x-forwarded-for`, so the load balancer's entry (Orchestrator's IP) was the only one.
> * When `KnownNetworks` is set, the .NET defaults (loopback) are kept, so local testing with a `localhost` client IP won't be granted roles.
> * The default `"AllNetworks"` accepts forwarded headers from any caller, so any client that can reach the API directly could spoof their IP.

### Management

The following appSetting configuration values are supported by `IIIFAuth2.Management`:

| Name                     | Description                                                                 | Default |
| ------------------------ | --------------------------------------------------------------------------- | ------- |
| Dlcs__ApiRoot            | Base URI for DLCS API, used to validate incoming credentials                |         |
| Dlcs__ApiDefaultTimeoutMs | Timeout for requests to DLCS API, in ms                                    | 30000   |
| AuthCacheTtl             | How long to cache successful credential checks for, in secs                 | 60      |
| RunMigrations            | If true, EF migrations will be run when app runs                            | `false` |
| AWS__SQS__CustomerCreatedQueueName | Name of SQS queue receiving DLCS customer-created notifications. Listener not started if empty |  |
| AWS__SQS__WaitTimeSecs   | Long-polling wait time for SQS receive, in secs                             | 20      |
| AWS__SQS__MaxNumberOfMessages | Max messages to fetch from SQS per request (1-10)                      | 10      |
| AWS__UseLocalStack       | If true, and environment is Development, use LocalStack for SQS             | `false` |
| AWS__SQS__ServiceUrl     | SQS service URL, only used with LocalStack                                  | `http://localhost:4566/` |

When a customer-created notification is received, a default `clickthrough` role-provider, access-service and role are created for that customer (see [bootstrap.sql](scripts/sql/bootstrap.sql) for equivalent SQL). The role id is `https://api.dlcs.io/customers/{id}/roles/clickthrough`. This is a no-op if the customer already has a `clickthrough` access-service.

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
2. In iiif-auth-v2 set `"OrchestratorRoot": "https://localhost:5041"` appSetting (nginx port)
3. In orchestrator set `"Auth__Auth2ServiceRoot": "https://localhost:7049/auth/v2/"` appSetting (default from auth-services launchSettings)
4. When making Orchestrator requests use the nginx port `:5041`, rather than the standard dotnet launchSettings port. This ensures that auth + orchestrator are on same port and avoids any cookie issues.

### One Off

You'll need to setup an access service and role provider locally, see [bootstrap.sql](./scripts/sql/bootstrap.sql) for example of setting up clickthrough provider.

### Test UI

`IIIFAuth2.TestUI` is a Razor Pages app that replaces Orchestrator + a IIIF client for local testing. Run it alongside IIIFAuth2.API (`https://localhost:7050`, no nginx or Orchestrator required):

* **Setup** (`/`) - register a deliverable id (`1/2/foo` or `99/100/page1.jpg/mets.xml`) and its roles. Roles are read from the `roles` table, or can be free-typed to simulate unknown roles. Deliverables are held in memory.
* **Auth flow** (`/Flow?id=...`) - a browser client that loads a fake manifest and runs probe -> access service -> token service -> probe -> content, logging each step along with downstream calls to iiif-auth-v2.
* **Downstream calls** (`/Calls`) - every request the fake Orchestrator made to iiif-auth-v2, with headers and bodies.

Fake Orchestrator endpoints:
* `/manifest/{deliverableId}` - single-canvas manifest, auth services from `/services/` endpoint.
* `/auth/v2/probe/{deliverableId}` - calls `/probe_internal/`, passing bearer token. Set iiif-auth-v2 `"OrchestratorRoot": "https://localhost:7050"` to have probe service ids point here, or leave the "send probe requests to this Test UI" option ticked. Returns 401 without calling iiif-auth-v2 if there's no bearer token, unless the deliverable has an `ip:` role.
* `/fake-image/{deliverableId}` - calls `/verifyaccess/`, forwarding the request `Cookie` header, returns an SVG showing result. Returns 401 without calling iiif-auth-v2 if there's no auth2 cookie for the customer, unless the deliverable has an `ip:` role.

If a deliverable has an `ip:` role, `X-Forwarded-For` is sent to `/probe_internal/` and `/verifyaccess/`, simulating the proposed Orchestrator changes for [IpRestriction roles](#role-provider-types). The value is the request IP, or a simulated client IP set on the Auth flow page (applies to all deliverables, held in memory). To test:

1. Run `scripts/sql/ip_restriction.sql` (edit the customer as required and remove the `rollback`), then restart IIIFAuth2.API as role and access-service records are cached.
2. Register a deliverable, e.g. `7/1/foo`, with the `ip:reading-room` role (optionally with a clickthrough role too).
3. On the Auth flow page, leave the client IP blank (localhost is in range) or set an IP in/out of the configured ranges, then run the flow. The probe returns 200 without a token if in range, else 403 "Not available at your location" (or 401 if there are other roles). Downstream calls show the `X-Forwarded-For` header sent.

Configured via `AuthServiceRoot` (default `https://localhost:7049/auth/v2/`) and `ConnectionStrings:Postgres` appSettings. The test UI needs to run on https on the same host as iiif-auth-v2 so the browser sends the auth cookie to the fake image endpoint (cookies ignore ports).

### Testing

There's a sample auth client available at https://tomcrane.github.io/iiif-auth-client/ that is useful for testing. This shows verbose logging to help diagnose and track any issues.

* https://tomcrane.github.io/iiif-auth-client/?image=https://localhost:5041/iiif-img/{asset-id}/info.json
* https://tomcrane.github.io/iiif-auth-client/?manifest=https://localhost:5041/iiif-manifest/{asset-id}

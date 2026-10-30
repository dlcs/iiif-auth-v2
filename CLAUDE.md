# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Implementation of the [IIIF Authorization Flow API 2.0](https://iiif.io/api/auth/2.0/) for DLCS ([RFC 012](https://github.com/dlcs/protagonist/blob/main/docs/rfcs/012-auth-service.md)). It is an extension of [DLCS Protagonist](https://github.com/dlcs/protagonist/) and is deployed under the same host as DLCS, with routing done at the load balancer. Orchestrator calls this service internally (probe, verify-access) and IIIF clients hit the public access/token endpoints.

.NET 10 solution at `src/IIIFAuth2/IIIFAuth2.sln`, using Postgres via EF Core (snake_case naming via EFCore.NamingConventions).

## Commands

Run from `src/IIIFAuth2`:

```bash
dotnet build
dotnet test                                              # all tests (CI: dotnet test ./src/IIIFAuth2/ /p:Configuration=Release)
dotnet test IIIFAuth2.API.Tests                          # one project
dotnet test --filter "FullyQualifiedName~VerifyAccessTests"   # single class / test

# add a migration (Core holds migrations, Management is the startup project)
dotnet ef migrations add "{migration-name}" -p IIIFAuth2.Core -s IIIFAuth2.Management -o Data/Migrations
```

Integration tests use Testcontainers (`postgres:18-alpine`), so **Docker must be running** to run tests.

Local infra (repo root): `docker compose -f docker-compose.db.yml up` (Postgres only), `docker-compose.local.yml` (Postgres + nginx on `https://localhost:5041` proxying `/auth/v2/*` to this API and everything else to a local Orchestrator), `docker-compose.local.certs.yml` (same, using exported .NET dev certs in `nginx/certs/`). `docker compose up` runs both built apps.

## Projects

- **IIIFAuth2.API** - public IIIF Auth 2 service (Dockerfile). Does **not** run migrations.
- **IIIFAuth2.Management** - internal management API (Dockerfile.Management). Owns migrations (applied at startup when `RunMigrations=true`), listens to an SQS customer-created queue to seed a default `clickthrough` role-provider/access-service/role per customer.
- **IIIFAuth2.Core** - shared `AuthServicesContext`, entities, migrations and domain models (e.g. `RoleProviderConfiguration`).
- **IIIFAuth2.TestUI** - Razor Pages app acting as fake Orchestrator + IIIF client for local end-to-end testing against the API without nginx/Orchestrator.
- **IIIFAuth2.Tests.Common** - `DatabaseFixture` (Postgres container, applies migrations, seeds customer 99 with clickthrough + oidc services).
- **IIIFAuth2.API.Tests / IIIFAuth2.Management.Tests** - xunit + FluentAssertions + FakeItEasy. Integration tests use `WebApplicationFactory<Program>` (see `TestingInfrastructure/AuthWebApplicationFactory.cs`) and share the DB via `[Collection(DatabaseCollection.CollectionName)]`.

## Architecture

### API request flow

Feature-folder layout (`Features/{Area}/`): thin controllers inheriting `AuthBaseController`, which wraps calls in `HandleRequest(...)` and dispatches MediatR requests in `Features/{Area}/Requests/`. Business logic lives in `Infrastructure/Auth`.

- `Probe` - `probe_internal/{**deliverableId}?roles=` - called by Orchestrator (passing bearer token) to evaluate a probe.
- `Presentation` - `services/{**deliverableId}` - IIIF auth service descriptions to embed in manifests/info.json.
- `Access` - `access/{customerId}/{accessServiceName}` (start login), `access/{customerId}/gesture` (POST significant gesture), `.../oauth2/callback`, `.../logout`, `access/{customerId}/token` (access token service, postMessage page).
- `VerifyAccess` - `verifyaccess/{**deliverableId}?roles=` - internal, returns status code only based on the request's auth cookie.

Deliverable ids are `{customer}/{space}/{asset}` or with an adjunct suffix.

### Role provisioning and sessions

`RoleProviderService` loads the `AccessService` + `RoleProvider` for a customer, picks the provider configuration for the current host (configurations can be keyed per host), then switches on `RoleProviderType`:
- `ClickThroughProviderHandler` - roles granted after significant gesture.
- `OidcRoleProviderHandler` (`RoleProvisioning/Oidc`) - redirects to external IdP, handles callback, validates JWT via JWKS (cached), maps claims to DLCS roles via `ClaimsConverter`. See `docs/identity_providers.md`.

`RoleProvisionGranter` creates a `SessionUser` and issues a per-customer cookie (`Auth:CookieNameFormat`); `SessionManagementService` / `SessionManagerBase` resolve sessions from cookie or bearer token and refresh expiry (`Auth:SessionTtl`, `Auth:RefreshThreshold`). `CustomerDomainService` + `CustomerCookieDomain` control which domains cookies are valid for. `UrlPathProvider` builds public URLs, honouring `OrchestratorRoot` and domain-specific path templates (`GesturePathTemplateForDomain`, `OAuthCallbackPathTemplateForDomain`).

### Management auth

All management routes are `/customers/{customerId}/...` and require authorization. A delegated auth handler forwards the incoming `Authorization` header to DLCS API `/customers/{customerId}` (`Dlcs:ApiRoot`); a 200 means authenticated, cached for `AuthCacheTtl`. Management won't start without `Dlcs:ApiRoot`.

## Conventions

- Config settings and their defaults are documented in `readme.md` - update the tables there when adding settings.
- PR template (`.github/pull_request_template.md`) has explicit sections for EF migrations, infrastructure changes, configuration changes and breaking HTTP contract changes - call these out.
- Sample setup SQL for a clickthrough provider: `scripts/sql/bootstrap.sql`.

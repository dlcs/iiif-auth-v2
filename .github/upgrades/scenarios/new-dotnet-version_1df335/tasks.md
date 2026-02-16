# IIIFAuth2 .NET 10.0 Upgrade Tasks

## Overview

This document tracks the execution of the IIIFAuth2 solution upgrade from .NET 6.0 to .NET 10.0 (LTS). Both projects will be upgraded simultaneously in a single atomic operation using the All-At-Once strategy, followed by testing and validation.

**Progress**: 0/3 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

---

## Tasks

### [▶] TASK-001: Verify prerequisites
**References**: Plan §Phase 1 - Pre-Migration Validation

- [ ] (1) Verify .NET 10.0 SDK installed per Plan §Prerequisites
- [ ] (2) SDK version meets minimum requirements (**Verify**)
- [ ] (3) Check global.json compatibility if present
- [ ] (4) global.json compatible with .NET 10.0 (**Verify**)

---

### [ ] TASK-002: Atomic framework and dependency upgrade
**References**: Plan §Phase 2-4, Plan §Package Update Reference, Plan §Breaking Changes Catalog

- [ ] (1) Update target framework to net10.0 in IIIFAuth2.API/IIIFAuth2.API.csproj
- [ ] (2) Update target framework to net10.0 in IIIFAuth2.API.Tests/IIIFAuth2.API.Tests.csproj
- [ ] (3) Both project files updated to net10.0 (**Verify**)
- [ ] (4) Remove deprecated MediatR.Extensions.Microsoft.DependencyInjection package from IIIFAuth2.API
- [ ] (5) Update Microsoft packages to 10.0.3 per Plan §Package Update Reference (Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.EntityFrameworkCore suite, Microsoft.AspNetCore.Mvc.Testing)
- [ ] (6) Update third-party packages per Plan §Package Update Reference (IdentityModel, IdentityModel.AspNetCore.OAuth2Introspection, Npgsql.EntityFrameworkCore.PostgreSQL to 10.0.0)
- [ ] (7) All packages updated to target versions (**Verify**)
- [ ] (8) Restore all dependencies
- [ ] (9) All dependencies restored successfully (**Verify**)
- [ ] (10) Build solution and fix all compilation errors per Plan §Breaking Changes Catalog (focus: IdentityModel binary incompatibilities, HttpContent strategy selection, System.Uri behavioral changes)
- [ ] (11) Solution builds with 0 errors (**Verify**)
- [ ] (12) Commit changes with message: "TASK-002: Atomic framework and dependency upgrade to .NET 10.0"

---

### [ ] TASK-003: Run full test suite and validate upgrade
**References**: Plan §Phase 5 - Testing & Validation

- [ ] (1) Run tests in IIIFAuth2.API.Tests project
- [ ] (2) Fix test failures per Plan §Breaking Changes Catalog (focus: HttpContent source incompatibilities, System.Uri behavioral changes in test assertions)
- [ ] (3) Re-run tests after fixes
- [ ] (4) All tests pass with 0 failures (**Verify**)
- [ ] (5) Commit test fixes with message: "TASK-003: Complete testing and validation"

---

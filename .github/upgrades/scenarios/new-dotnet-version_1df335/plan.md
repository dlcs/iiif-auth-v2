# .NET 10.0 Upgrade Plan

## Table of Contents

- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Project-by-Project Plans](#project-by-project-plans)
  - [IIIFAuth2.API](#iiifauth2api)
  - [IIIFAuth2.API.Tests](#iiifauth2apitests)
- [Package Update Reference](#package-update-reference)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Risk Management](#risk-management)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)

---

## Executive Summary

This plan outlines the upgrade of the IIIFAuth2 solution from **.NET 6.0** to **.NET 10.0 (LTS)**. The solution consists of 2 projects:

- **IIIFAuth2.API** - ASP.NET Core Razor Pages application
- **IIIFAuth2.API.Tests** - xUnit test project

### Key Metrics

- **Projects**: 2 (1 web application, 1 test project)
- **Total NuGet Packages**: 26
- **Packages Requiring Updates**: 5
  - 4 packages recommended for upgrade
  - 1 deprecated package requiring replacement
- **API Compatibility Issues**: 214 total
  - 8 binary incompatible changes
  - 11 source incompatible changes
  - 195 behavioral changes
- **Overall Complexity**: **Low**

### Strategic Approach

**All-At-Once Migration Strategy** - Given the small solution size, simple dependency structure, and low complexity rating, we'll upgrade both projects simultaneously in a single coordinated effort. This minimizes intermediate states and reduces overall migration time.

### Timeline Estimate

- **Assessment**: ✓ Complete
- **Planning**: ✓ In Progress
- **Execution**: ~2-4 hours (including testing and validation)

### Critical Success Factors

1. Replace deprecated `MediatR.Extensions.Microsoft.DependencyInjection` package
2. Update 4 packages to .NET 10.0-compatible versions
3. Address binary incompatibilities in IdentityModel APIs
4. Validate behavioral changes in System.Uri and HttpContent
5. Ensure all tests pass after migration

---

## Migration Strategy

### Strategy Selection: All-At-Once

**Rationale:**
- Small solution (only 2 projects)
- Simple linear dependency chain (API → Tests)
- Both projects rated Low complexity
- No intermediate .NET versions to consider (jumping from 6.0 to 10.0)
- Minimal risk of multi-version conflicts

### Migration Phases

#### Phase 1: Pre-Migration Validation
1. Validate .NET 10.0 SDK installation
2. Check global.json compatibility
3. Backup current state (Git commit checkpoint)
4. Run baseline test suite

#### Phase 2: Core Project Migration (IIIFAuth2.API)
1. Update target framework to `net10.0`
2. Update NuGet packages
   - Replace deprecated `MediatR.Extensions.Microsoft.DependencyInjection` with `MediatR`
   - Update Microsoft.* packages to 10.0.3
   - Update test-related packages
3. Address binary incompatibilities
4. Build and validate

#### Phase 3: Test Project Migration (IIIFAuth2.API.Tests)
1. Update target framework to `net10.0`
2. Update test-related packages
3. Build and validate

#### Phase 4: Code Compatibility Fixes
1. Review and address API breaking changes
2. Address behavioral changes
3. Update deprecated API usage

#### Phase 5: Validation & Testing
1. Run full test suite
2. Validate application startup
3. Smoke test critical paths
4. Review build warnings

#### Phase 6: Finalization
1. Final build verification
2. Documentation updates
3. Commit migration changes

### Rollback Strategy

If critical issues arise:
1. Revert to `feature/refresh` branch
2. Analyze failure points from plan.md
3. Create targeted fix strategy
4. Re-attempt migration

### Dependencies Between Phases

- Phase 2 must complete before Phase 3 (test project depends on API project)
- Phase 4 can only begin after Phase 3 (need both projects on .NET 10.0)
- Phase 5 requires Phase 4 completion

---

## Detailed Dependency Analysis

### Project Dependency Graph

```
IIIFAuth2.API (Web Application - Razor Pages)
    ↓ (depends on)
IIIFAuth2.API.Tests (Test Project)
```

**Upgrade Order**: IIIFAuth2.API → IIIFAuth2.API.Tests

### IIIFAuth2.API Dependencies

**Current Target Framework**: net6.0  
**Target Framework**: net10.0  
**Complexity**: Low  
**Dependencies**: 0 project dependencies  
**Dependants**: 1 (IIIFAuth2.API.Tests)

**NuGet Packages** (22 total):
- IdentityModel (7.0.0) ⚠️ Upgrade recommended
- IdentityModel.AspNetCore.OAuth2Introspection (7.0.0) ⚠️ Upgrade recommended
- MediatR (12.4.1) ✓ Compatible
- MediatR.Extensions.Microsoft.DependencyInjection (11.0.0) ❌ Deprecated
- Microsoft.AspNetCore.Authentication.JwtBearer (8.0.11) ⚠️ Upgrade recommended
- Microsoft.EntityFrameworkCore (8.0.11) ⚠️ Upgrade recommended
- Microsoft.EntityFrameworkCore.Design (8.0.11) ⚠️ Upgrade recommended
- Microsoft.EntityFrameworkCore.SqlServer (8.0.11) ⚠️ Upgrade recommended
- Microsoft.EntityFrameworkCore.Tools (8.0.11) ⚠️ Upgrade recommended
- Npgsql.EntityFrameworkCore.PostgreSQL (8.0.11) ⚠️ Upgrade recommended
- Serilog (4.1.0) ✓ Compatible
- Serilog.AspNetCore (8.0.3) ✓ Compatible
- Serilog.Enrichers.Environment (3.1.0) ✓ Compatible
- Serilog.Enrichers.Process (3.0.0) ✓ Compatible
- Serilog.Enrichers.Thread (4.0.0) ✓ Compatible
- Serilog.Exceptions (8.4.0) ✓ Compatible
- Serilog.Sinks.Console (6.0.0) ✓ Compatible
- Serilog.Sinks.Seq (8.0.0) ✓ Compatible
- Swashbuckle.AspNetCore (7.2.0) ✓ Compatible

### IIIFAuth2.API.Tests Dependencies

**Current Target Framework**: net6.0  
**Target Framework**: net10.0  
**Complexity**: Low  
**Project Dependencies**: IIIFAuth2.API

**NuGet Packages** (7 total):
- coverlet.collector (6.0.2) ✓ Compatible
- Microsoft.AspNetCore.Mvc.Testing (8.0.11) ⚠️ Upgrade recommended
- Microsoft.NET.Test.Sdk (17.11.1) ✓ Compatible
- xunit (2.9.2) ✓ Compatible
- xunit.runner.visualstudio (2.8.2) ✓ Compatible

### Critical Package Actions

#### 1. Deprecated Package Replacement
**MediatR.Extensions.Microsoft.DependencyInjection** (v11.0.0)
- **Status**: Deprecated
- **Action**: Remove package, functionality now included in MediatR core package (v12.4.1)
- **Impact**: Minimal - MediatR 12.x includes DI extensions natively
- **Code Changes**: None required if using `AddMediatR()` extension

#### 2. Microsoft Framework Packages (Priority: High)
Update from 8.0.11 → 10.0.3:
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.Mvc.Testing
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Design
- Microsoft.EntityFrameworkCore.SqlServer
- Microsoft.EntityFrameworkCore.Tools

#### 3. Third-Party Package Updates
- **IdentityModel**: 7.0.0 → Latest compatible version
- **IdentityModel.AspNetCore.OAuth2Introspection**: 7.0.0 → Latest compatible version
- **Npgsql.EntityFrameworkCore.PostgreSQL**: 8.0.11 → 10.0.0

---

## Project-by-Project Plans

### IIIFAuth2.API

**Project Type**: ASP.NET Core Web Application (Razor Pages)  
**Current Framework**: net6.0  
**Target Framework**: net10.0  
**Complexity**: Low  
**Project File**: `IIIFAuth2.API\IIIFAuth2.API.csproj`

#### Issues Summary
- **Binary Incompatible**: 8 instances
- **Source Incompatible**: 11 instances
- **Behavioral Changes**: 195 instances
- **Package Updates**: 4 required, 1 deprecated
- **Framework Change**: Required

#### Critical Breaking Changes

##### 1. IdentityModel Binary Incompatibilities (8 instances)

**Issue**: Binary incompatible changes in IdentityModel APIs

**Affected APIs**:
- `IdentityModel.Client.DiscoveryDocumentResponse`
- `IdentityModel.Client.HttpClientDiscoveryExtensions`
- `IdentityModel.Client.HttpClientTokenRequestExtensions`
- `IdentityModel.Client.TokenResponse`

**Files Impacted**:
- `Services/TokenService.cs` (if exists)
- `Authentication/*.cs` files
- Any OAuth2/OIDC client code

**Resolution Strategy**:
1. Update IdentityModel package to latest .NET 10 compatible version
2. Review API usage in affected files
3. Update method signatures/properties per IdentityModel migration guide
4. Test authentication flows thoroughly

##### 2. System.Uri Behavioral Changes (195 instances)

**Issue**: Behavioral change in `System.Uri` formatting and parsing

**Impact**: URI string representations may differ, affecting:
- Redirect URIs
- API endpoint construction
- URL comparison logic

**Resolution Strategy**:
1. Review URI construction in authentication/authorization code
2. Test redirect flows
3. Verify API client URL generation
4. Update unit tests with new expected formats

##### 3. HttpContent Strategy Selection (11 instances)

**Issue**: Source incompatible - `System.Net.Http.HttpContent` strategy selection has changed

**Files Potentially Affected**:
- `Services/*Client.cs`
- `Extensions/HttpClientExtensions.cs`
- API client implementations

**Resolution Strategy**:
1. Review HttpContent usage patterns
2. Update to explicit content type selection
3. Test HTTP client calls

#### Package Update Plan

1. **Remove Deprecated Package**
   ```xml
   <!-- Remove -->
   <PackageReference Include="MediatR.Extensions.Microsoft.DependencyInjection" Version="11.0.0" />
   ```
   Note: Functionality now in MediatR core (v12.4.1)

2. **Update Microsoft Packages** (8.0.11 → 10.0.3)
   ```xml
   <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.3" />
   <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.3" />
   <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.3" />
   <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.3" />
   <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.3" />
   ```

3. **Update Third-Party Packages**
   ```xml
   <PackageReference Include="IdentityModel" Version="[latest-net10-compatible]" />
   <PackageReference Include="IdentityModel.AspNetCore.OAuth2Introspection" Version="[latest-net10-compatible]" />
   <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
   ```

#### Target Framework Update

```xml
<!-- From -->
<TargetFramework>net6.0</TargetFramework>

<!-- To -->
<TargetFramework>net10.0</TargetFramework>
```

#### Build & Validation Checklist

- [ ] Update target framework
- [ ] Remove deprecated MediatR.Extensions package
- [ ] Update all Microsoft.* packages
- [ ] Update third-party packages
- [ ] Build project (resolve compilation errors)
- [ ] Address binary incompatibilities
- [ ] Run code analysis
- [ ] Review and address warnings

### IIIFAuth2.API.Tests

**Project Type**: xUnit Test Project  
**Current Framework**: net6.0  
**Target Framework**: net10.0  
**Complexity**: Low  
**Project File**: `IIIFAuth2.API.Tests\IIIFAuth2.API.Tests.csproj`  
**Dependencies**: IIIFAuth2.API (project reference)

#### Issues Summary
- **Binary Incompatible**: 0 instances
- **Source Incompatible**: 11 instances
- **Behavioral Changes**: 195 instances
- **Package Updates**: 1 required
- **Framework Change**: Required

#### Critical Changes

##### 1. HttpContent Strategy Selection (11 instances)

**Issue**: Same as main project - source incompatible changes in `System.Net.Http.HttpContent`

**Impact**: Test code using HttpClient/HttpContent may need updates

**Resolution Strategy**:
1. Review integration test HTTP calls
2. Update mock/fake HTTP content creation
3. Verify test assertions still valid

##### 2. System.Uri Behavioral Changes (195 instances)

**Issue**: Same as main project - URI formatting changes

**Impact**: Test assertions comparing URIs may fail

**Resolution Strategy**:
1. Review URI assertions in tests
2. Update expected values
3. Consider using URI equality instead of string comparison

#### Package Update Plan

1. **Update Microsoft Test Package** (8.0.11 → 10.0.3)
   ```xml
   <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.3" />
   ```

2. **Verify Test Framework Compatibility**
   - xunit (2.9.2) - ✓ Compatible with .NET 10
   - xunit.runner.visualstudio (2.8.2) - ✓ Compatible
   - Microsoft.NET.Test.Sdk (17.11.1) - ✓ Compatible
   - coverlet.collector (6.0.2) - ✓ Compatible

#### Target Framework Update

```xml
<!-- From -->
<TargetFramework>net6.0</TargetFramework>

<!-- To -->
<TargetFramework>net10.0</TargetFramework>
```

#### Build & Validation Checklist

- [ ] Update target framework
- [ ] Update Microsoft.AspNetCore.Mvc.Testing package
- [ ] Build project
- [ ] Run all tests
- [ ] Address test failures related to behavioral changes
- [ ] Verify code coverage maintained

---

## Package Update Reference

### Packages Requiring Action

| Package Name | Current Version | Target Version | Action | Priority | Projects Affected |
|-------------|-----------------|----------------|---------|----------|-------------------|
| **MediatR.Extensions.Microsoft.DependencyInjection** | 11.0.0 | N/A | **REMOVE** (Deprecated) | High | IIIFAuth2.API |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.11 | 10.0.3 | Update | High | IIIFAuth2.API |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.11 | 10.0.3 | Update | High | IIIFAuth2.API.Tests |
| Microsoft.EntityFrameworkCore | 8.0.11 | 10.0.3 | Update | High | IIIFAuth2.API |
| Microsoft.EntityFrameworkCore.Design | 8.0.11 | 10.0.3 | Update | High | IIIFAuth2.API |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 | 10.0.3 | Update | High | IIIFAuth2.API |
| Microsoft.EntityFrameworkCore.Tools | 8.0.11 | 10.0.3 | Update | High | IIIFAuth2.API |
| Npgsql.EntityFrameworkCore.PostgreSQL | 8.0.11 | 10.0.0 | Update | High | IIIFAuth2.API |
| IdentityModel | 7.0.0 | TBD | Update | High | IIIFAuth2.API |
| IdentityModel.AspNetCore.OAuth2Introspection | 7.0.0 | TBD | Update | High | IIIFAuth2.API |

### Packages Compatible (No Action Required)

| Package Name | Version | Projects |
|-------------|---------|----------|
| MediatR | 12.4.1 | IIIFAuth2.API |
| Serilog | 4.1.0 | IIIFAuth2.API |
| Serilog.AspNetCore | 8.0.3 | IIIFAuth2.API |
| Serilog.Enrichers.Environment | 3.1.0 | IIIFAuth2.API |
| Serilog.Enrichers.Process | 3.0.0 | IIIFAuth2.API |
| Serilog.Enrichers.Thread | 4.0.0 | IIIFAuth2.API |
| Serilog.Exceptions | 8.4.0 | IIIFAuth2.API |
| Serilog.Sinks.Console | 6.0.0 | IIIFAuth2.API |
| Serilog.Sinks.Seq | 8.0.0 | IIIFAuth2.API |
| Swashbuckle.AspNetCore | 7.2.0 | IIIFAuth2.API |
| coverlet.collector | 6.0.2 | IIIFAuth2.API.Tests |
| Microsoft.NET.Test.Sdk | 17.11.1 | IIIFAuth2.API.Tests |
| xunit | 2.9.2 | IIIFAuth2.API.Tests |
| xunit.runner.visualstudio | 2.8.2 | IIIFAuth2.API.Tests |

### Deprecated Package Details

#### MediatR.Extensions.Microsoft.DependencyInjection

**Why Deprecated**: MediatR v12+ includes DI extensions in the core package

**Migration Path**:
1. Remove package reference
2. Verify `MediatR` package is v12.4.1 or higher (✓ already at 12.4.1)
3. No code changes needed - `AddMediatR()` method available from core package

**Code Impact**: None - registration code remains identical

### Package Version Resolution Strategy

For packages marked "TBD":
1. Query NuGet for latest stable version compatible with net10.0
2. Check for breaking changes in release notes
3. Update to highest stable version
4. Test thoroughly before proceeding

---

## Breaking Changes Catalog

### Binary Incompatible Changes (8 instances)

#### IdentityModel APIs

**Severity**: High  
**Affected Projects**: IIIFAuth2.API  
**Instance Count**: 8

**Changed APIs**:
1. `IdentityModel.Client.DiscoveryDocumentResponse`
2. `IdentityModel.Client.HttpClientDiscoveryExtensions`
3. `IdentityModel.Client.HttpClientTokenRequestExtensions`
4. `IdentityModel.Client.TokenResponse`

**Symptoms**:
- Compilation may succeed but runtime failures possible
- Method signatures changed
- Property access patterns changed
- Return types may have changed

**Detection Strategy**:
1. Search codebase for `DiscoveryDocumentResponse` usage
2. Find `HttpClient` extension method calls for discovery/token
3. Locate `TokenResponse` handling code

**Resolution Approach**:
1. Update IdentityModel package first
2. Review compiler warnings/errors
3. Consult IdentityModel migration guide
4. Update method calls to match new signatures
5. Test OAuth2/OIDC flows end-to-end

**Testing Requirements**:
- Token acquisition flows
- Discovery document retrieval
- Token validation
- Authentication middleware functionality

---

### Source Incompatible Changes (11 instances)

#### HttpContent Strategy Selection

**Severity**: Medium  
**Affected Projects**: IIIFAuth2.API, IIIFAuth2.API.Tests  
**Instance Count**: 11 (combined)

**Changed API**: `System.Net.Http.HttpContent`

**What Changed**:
- Content type strategy selection mechanism changed in .NET 10
- May affect implicit content type detection
- Could impact custom HttpContent implementations

**Likely Locations**:
```csharp
// API clients
var content = new StringContent(json, Encoding.UTF8, "application/json");

// Custom content types
public class CustomContent : HttpContent { }

// Extension methods
httpClient.PostAsync(url, content);
```

**Resolution Steps**:
1. Locate all `HttpContent` instantiations
2. Ensure explicit content type specification
3. Review custom HttpContent subclasses
4. Update media type headers explicitly
5. Test all HTTP client calls

**Testing Requirements**:
- API request/response cycles
- Content negotiation
- Custom content type handling

---

### Behavioral Changes (195 instances)

#### System.Uri Formatting Changes

**Severity**: Medium  
**Affected Projects**: IIIFAuth2.API, IIIFAuth2.API.Tests  
**Instance Count**: 195 (combined)

**What Changed**:
- URI string representation format changes
- Percent-encoding behavior differences
- Query string parsing variations
- Fragment handling updates

**Common Impact Areas**:
```csharp
// URI construction for OAuth2 redirects
var redirectUri = new Uri(baseUri, "signin-oidc");

// API endpoint building
var apiEndpoint = new Uri($"{baseUrl}/api/resource");

// URI comparison
if (uri1.ToString() == uri2.ToString()) // May fail

// Query parameter handling
var builder = new UriBuilder(uri);
builder.Query = "param=value";
```

**Resolution Strategy**:
1. **Authentication/Authorization**:
   - Test OAuth2 redirect URIs
   - Verify callback URL construction
   - Check JWT issuer/audience claims (often URIs)

2. **API Clients**:
   - Review endpoint URL construction
   - Verify query string building
   - Test URI-based routing

3. **Unit Tests**:
   - Update URI string assertions
   - Use `Uri.Equals()` instead of string comparison
   - Update expected URI formats in test data

4. **General Code**:
   - Audit `Uri.ToString()` usage
   - Review URI comparison logic
   - Check URI logging/diagnostics

**Testing Requirements**:
- OAuth2 authentication flows
- API client calls
- Redirect handling
- URI-based assertions in tests
- Any URI string comparisons

**Mitigation**:
```csharp
// Before
Assert.Equal("https://example.com/path", uri.ToString());

// After - prefer Uri comparison
Assert.Equal(expectedUri, actualUri);

// Or normalize string comparison
Assert.Equal(expectedUri.ToString(), actualUri.ToString());
```

---

### Summary by Severity

| Severity | Category | Count | Projects | Mitigation Complexity |
|----------|----------|-------|----------|----------------------|
| **High** | Binary Incompatible | 8 | API | Medium - Package update + code changes |
| **Medium** | Source Incompatible | 11 | API, Tests | Low - Explicit type specification |
| **Medium** | Behavioral Changes | 195 | API, Tests | Low-Medium - Test updates, URI handling |

### Breaking Change Resolution Order

1. **First**: Update all packages (especially IdentityModel)
2. **Second**: Address binary incompatibilities (compiler will guide)
3. **Third**: Fix source incompatibilities (build errors)
4. **Fourth**: Test and address behavioral changes (runtime testing)
5. **Final**: Update unit test assertions

---

## Risk Management

### Risk Assessment Matrix

| Risk | Likelihood | Impact | Severity | Mitigation |
|------|-----------|---------|----------|------------|
| IdentityModel breaking changes break authentication | Medium | High | **HIGH** | Test auth flows thoroughly; have rollback plan |
| URI behavioral changes break OAuth redirects | Medium | High | **HIGH** | Comprehensive redirect testing; monitor logs |
| HttpContent changes break API clients | Low | Medium | **MEDIUM** | Test all HTTP client usage |
| Package version conflicts | Low | Medium | **MEDIUM** | Use compatible version ranges |
| Test failures due to behavioral changes | High | Low | **MEDIUM** | Allocate time for test updates |
| EF Core migration compatibility issues | Low | High | **MEDIUM** | Backup database; test migrations |
| Performance regressions | Low | Low | **LOW** | Benchmark critical paths |

### High-Risk Areas

#### 1. Authentication & Authorization ⚠️ HIGH RISK

**Why Risky**:
- 8 binary incompatibilities in IdentityModel
- OAuth2 flows depend on URI handling (195 behavioral changes)
- JWT validation is critical for security

**Mitigation Plan**:
- [ ] Create comprehensive auth test suite before migration
- [ ] Test all authentication flows:
  - [ ] Token acquisition
  - [ ] Token refresh
  - [ ] Token introspection
  - [ ] Discovery document retrieval
  - [ ] JWT validation
- [ ] Monitor authentication logs during testing
- [ ] Verify redirect URIs manually
- [ ] Test with real OAuth2 providers if possible

**Rollback Trigger**: Any authentication flow failure

#### 2. Database Layer (Entity Framework Core)

**Why Risky**:
- Upgrading EF Core from 8.0.11 to 10.0.3
- Database provider compatibility (Npgsql, SQL Server)
- Potential query translation changes

**Mitigation Plan**:
- [ ] Backup development database
- [ ] Review EF Core 9.0 and 10.0 breaking changes
- [ ] Test all database queries
- [ ] Verify migrations compatibility
- [ ] Test both PostgreSQL and SQL Server providers
- [ ] Monitor query performance

**Rollback Trigger**: Migration failures or data access errors

#### 3. API Clients & HTTP Communication

**Why Risky**:
- HttpContent source incompatibilities
- URI formatting changes may affect external API calls

**Mitigation Plan**:
- [ ] Identify all HTTP client usage
- [ ] Test API client calls
- [ ] Verify content type handling
- [ ] Test error handling paths

**Rollback Trigger**: External API communication failures

### Medium-Risk Areas

#### 4. Test Suite Maintenance

**Expected Impact**: Test assertions may fail due to behavioral changes

**Mitigation**:
- Allocate 30-40% of migration time to test updates
- Update expected values for URI assertions
- Review all string-based URI comparisons

#### 5. Third-Party Package Compatibility

**Concern**: Serilog, Swashbuckle, xUnit versions may have subtle issues

**Mitigation**:
- Test logging functionality
- Verify Swagger UI works
- Run full test suite

### Rollback Plan

#### Trigger Conditions

Rollback if any of these occur:
1. Authentication completely broken
2. Database access failures
3. More than 50% of tests failing
4. Critical runtime errors in core functionality
5. Cannot resolve package version conflicts

#### Rollback Steps

```bash
# Stop any running instances
# Switch back to source branch
git checkout feature/refresh

# Discard migration branch changes (if needed)
git branch -D upgrade-to-NET10

# Verify application works
dotnet build
dotnet test
```

#### Recovery Time Objective (RTO)

- **Rollback Time**: < 5 minutes
- **Investigation Time**: 1-2 hours to analyze failures
- **Retry Time**: 2-4 hours for targeted fixes

### Risk Reduction Strategies

1. **Incremental Testing**: Test after each phase
2. **Logging**: Enable detailed logging during migration testing
3. **Automated Tests**: Lean heavily on existing test suite
4. **Manual Testing**: Test critical user journeys manually
5. **Staged Approach**: If high-risk issues found, consider stopping and re-planning

### Success Probability Assessment

**Overall Success Probability**: **85%**

**Confidence Factors**:
- ✅ Small solution (2 projects)
- ✅ Low complexity rating
- ✅ SDK-style projects
- ✅ Comprehensive plan
- ✅ Clear rollback strategy

**Risk Factors**:
- ⚠️ IdentityModel breaking changes
- ⚠️ Large number of behavioral changes (195)
- ⚠️ Authentication is critical security component

---

## Testing & Validation Strategy

### Pre-Migration Baseline

**Purpose**: Establish known-good state for comparison

#### Actions
1. **Capture Current Test Results**
   ```bash
   dotnet test --logger "console;verbosity=detailed" > pre-migration-tests.log
   ```
   - Record pass/fail counts
   - Note any existing failures (not migration-related)

2. **Document Current Behavior**
   - [ ] Application starts successfully
   - [ ] Authentication flows work
   - [ ] Database connectivity confirmed
   - [ ] API endpoints respond correctly

3. **Performance Baseline** (optional)
   - Record startup time
   - Measure key endpoint response times

### Migration Testing Phases

#### Phase 1: Build Validation (After Each Project)

**Trigger**: After updating each project's TFM and packages

**Tests**:
```bash
# Clean build
dotnet clean
dotnet build --configuration Debug

# Check for warnings
dotnet build --configuration Release -warnaserror
```

**Success Criteria**:
- ✅ Zero compilation errors
- ✅ Zero critical warnings
- ✅ All projects build successfully

**Failure Response**: 
- Fix compilation errors before proceeding
- Review package compatibility issues
- Check for API incompatibilities

---

#### Phase 2: Unit Test Validation

**Trigger**: After both projects migrated and building

**Tests**:
```bash
# Run all tests
dotnet test --configuration Debug --verbosity normal

# With coverage
dotnet test --configuration Debug --collect:"XPlat Code Coverage"
```

**Success Criteria**:
- ✅ Same or more tests passing vs baseline
- ✅ Zero new test failures (existing failures OK)
- ✅ Code coverage maintained or improved

**Expected Issues**:
- URI string assertion failures → Update expected values
- HTTP content type assertions → Update expectations
- Mock setup issues → Update to new API signatures

**Failure Response**:
- Categorize failures: Breaking change vs actual bug
- Update test assertions for behavioral changes
- Fix code for genuine compatibility issues
- Document any deferred test fixes

---

#### Phase 3: Integration Testing

**Trigger**: After unit tests pass

**Manual Test Checklist**:

##### Application Startup
- [ ] Application starts without errors
- [ ] No unhandled exceptions in logs
- [ ] Health checks pass (if implemented)
- [ ] Swagger UI loads correctly

##### Authentication & Authorization
- [ ] Can acquire access token
- [ ] Token introspection works
- [ ] Token refresh works (if implemented)
- [ ] Protected endpoints reject anonymous requests
- [ ] Protected endpoints accept valid tokens
- [ ] Invalid tokens are rejected with 401
- [ ] Insufficient permissions return 403

##### Database Operations
- [ ] Database connection established
- [ ] EF Core migrations work
- [ ] Read operations successful
- [ ] Write operations successful
- [ ] Transactions work correctly
- [ ] Query results match expectations

##### API Functionality
- [ ] GET requests return data
- [ ] POST requests create resources
- [ ] PUT requests update resources
- [ ] DELETE requests remove resources
- [ ] Content negotiation works
- [ ] Error responses formatted correctly

##### Logging & Monitoring
- [ ] Serilog writes to console
- [ ] Serilog writes to Seq (if configured)
- [ ] Log enrichment working (environment, process, thread)
- [ ] Exception logging captures details

---

#### Phase 4: Behavioral Change Validation

**Focus**: URI and HttpContent behavioral changes

##### URI Testing
```csharp
// Test redirect URIs
var redirectUri = new Uri(baseUri, "signin-oidc");
// Verify: No double slashes, correct encoding, expected format

// Test API endpoints
var apiUri = new Uri($"{baseUrl}/api/resource?id=123");
// Verify: Query parameters correct, encoding proper

// Test URI comparison
// Use Uri.Equals() instead of string comparison
```

**Validation**:
- [ ] OAuth redirect URIs formatted correctly
- [ ] No broken redirects in authentication flow
- [ ] API client URLs constructed properly
- [ ] Query parameters encoded correctly

##### HttpContent Testing
- [ ] JSON content posted correctly
- [ ] Content-Type headers correct
- [ ] Form data handled properly
- [ ] Custom content types work

---

#### Phase 5: Performance & Regression Testing

**Optional but Recommended**

**Metrics to Compare**:
- Application startup time
- First request response time
- Database query performance
- Memory usage
- CPU usage under load

**Acceptance**:
- No significant performance degradation (>10%)
- Memory leaks not introduced
- Startup time similar or better

---

### Test Automation Strategy

#### Automated Test Suite

**Current Tests**: xUnit tests in IIIFAuth2.API.Tests

**Execution Frequency**:
- After each code change
- Before committing
- In CI/CD pipeline (if configured)

**Coverage Goals**:
- Maintain existing coverage percentage
- Add tests for migration-specific concerns if gaps found

#### CI/CD Integration (if applicable)

```yaml
# Example pipeline step
- name: Build and Test
  run: |
    dotnet restore
    dotnet build --configuration Release
    dotnet test --configuration Release --logger trx --collect:"XPlat Code Coverage"
```

---

### Test Result Documentation

#### Test Report Template

```markdown
## Migration Test Results - [Date]

### Build Status
- IIIFAuth2.API: ✅/❌
- IIIFAuth2.API.Tests: ✅/❌

### Unit Tests
- Total: [count]
- Passed: [count]
- Failed: [count]
- Skipped: [count]

### Integration Tests
- Authentication: ✅/❌
- Database: ✅/❌
- API Endpoints: ✅/❌
- Logging: ✅/❌

### Behavioral Changes
- URI Handling: ✅/❌
- HttpContent: ✅/❌

### Issues Found
1. [Issue description]
   - Severity: High/Medium/Low
   - Resolution: [planned action]

### Next Steps
- [Action items]
```

---

### Validation Checkpoints

| Checkpoint | Phase | Must Pass | Can Defer |
|------------|-------|-----------|-----------|
| Build successful | 1 | ✅ | - |
| No compilation errors | 1 | ✅ | - |
| Unit tests pass | 2 | ✅ | - |
| Application starts | 3 | ✅ | - |
| Authentication works | 3 | ✅ | - |
| Database queries work | 3 | ✅ | - |
| API endpoints respond | 3 | ✅ | - |
| URI formats correct | 4 | ✅ | - |
| No performance regression | 5 | - | ✅ |
| All warnings resolved | All | - | ✅ |

---

### Test Failure Response Plan

#### Category 1: Build Failures
- **Priority**: Critical
- **Action**: Fix immediately, do not proceed
- **Common Causes**: Package incompatibility, API breaking changes

#### Category 2: Test Failures
- **Priority**: High
- **Action**: Analyze and categorize
  - Behavioral change → Update test
  - Real bug → Fix code
  - Flaky test → Investigate separately

#### Category 3: Integration Issues
- **Priority**: High
- **Action**: Debug and resolve
- **Rollback Trigger**: If critical path broken

#### Category 4: Performance Issues
- **Priority**: Medium
- **Action**: Investigate after functional issues resolved
- **Acceptable**: Minor variations (<10%)

---

### Sign-Off Criteria

Migration is considered successful when:
- ✅ All projects build without errors
- ✅ Unit test pass rate >= baseline
- ✅ Critical authentication flows work
- ✅ Database operations functional
- ✅ API endpoints respond correctly
- ✅ No critical runtime errors
- ✅ Logging works as expected

---

## Complexity & Effort Assessment

### Overall Complexity: **LOW**

**Justification**:
- Small solution (2 projects only)
- Both projects already SDK-style (no conversion needed)
- Simple dependency chain (API → Tests)
- Modern packages (most already .NET 8 compatible)
- Well-understood migration path (.NET 6 → 10)

### Complexity Breakdown by Project

| Project | Complexity | Justification |
|---------|-----------|---------------|
| IIIFAuth2.API | **Low** | Modern ASP.NET Core, standard patterns, SDK-style |
| IIIFAuth2.API.Tests | **Low** | Standard xUnit tests, minimal dependencies |

### Effort Estimation

#### Time Breakdown

| Phase | Estimated Time | Confidence |
|-------|----------------|------------|
| **Phase 1**: Pre-Migration Validation | 15 minutes | High |
| **Phase 2**: Core Project Migration | 45 minutes | High |
| **Phase 3**: Test Project Migration | 30 minutes | High |
| **Phase 4**: Code Compatibility Fixes | 60-120 minutes | Medium |
| **Phase 5**: Testing & Validation | 60-90 minutes | Medium |
| **Phase 6**: Finalization | 15 minutes | High |
| **Buffer**: Unexpected issues | 30-60 minutes | Low |
| **TOTAL** | **4-6 hours** | Medium-High |

#### Phase Details

##### Phase 1: Pre-Migration Validation (15 min)
- ✅ Verify .NET 10 SDK installed: 2 min
- ✅ Check global.json: 3 min
- ✅ Run baseline tests: 10 min

##### Phase 2: Core Project Migration (45 min)
- Update TFM in .csproj: 2 min
- Remove deprecated MediatR package: 2 min
- Update Microsoft packages (8 packages): 10 min
- Update third-party packages (3 packages): 10 min
- Build and resolve errors: 15 min
- Initial validation: 6 min

##### Phase 3: Test Project Migration (30 min)
- Update TFM in .csproj: 2 min
- Update test packages (1 package): 5 min
- Build project: 3 min
- Run tests (expect failures): 10 min
- Quick fixes: 10 min

##### Phase 4: Code Compatibility Fixes (60-120 min)
**High Variance - Depends on actual breaking changes**

- IdentityModel API updates: 30-60 min
  - Find usage locations: 10-15 min
  - Update code: 15-30 min
  - Test authentication: 5-15 min

- URI behavioral changes: 15-30 min
  - Review URI usage: 10-15 min
  - Update test assertions: 5-15 min

- HttpContent fixes: 15-30 min
  - Find HttpContent usage: 5-10 min
  - Update explicit types: 10-20 min

##### Phase 5: Testing & Validation (60-90 min)
- Unit test runs: 20-30 min
- Manual integration testing: 30-45 min
- Authentication flow testing: 10-15 min

##### Phase 6: Finalization (15 min)
- Final build check: 5 min
- Update documentation: 5 min
- Commit changes: 5 min

---

### Complexity Factors

#### Factors Reducing Complexity ✅

1. **Small Codebase**: Only 2 projects
2. **SDK-Style Projects**: No project file conversion needed
3. **Modern Stack**: Already on .NET 6, most packages modern
4. **Clear Dependencies**: Linear dependency chain
5. **Good Package Hygiene**: Most packages already compatible
6. **Standard Patterns**: ASP.NET Core follows Microsoft patterns
7. **Test Coverage**: Existing test project for validation

#### Factors Increasing Complexity ⚠️

1. **Binary Incompatibilities**: 8 instances in IdentityModel (authentication-critical)
2. **High Behavioral Change Count**: 195 instances (mostly URI-related)
3. **Authentication Component**: Security-critical, requires thorough testing
4. **Multiple Database Providers**: SQL Server + PostgreSQL (2x testing)
5. **Deprecated Package**: Manual replacement required

---

### Skill Requirements

| Skill Area | Required Level | Importance |
|------------|---------------|------------|
| C# / .NET | Intermediate | High |
| ASP.NET Core | Intermediate | High |
| Entity Framework Core | Basic | Medium |
| OAuth2 / OIDC | Basic-Intermediate | High |
| NuGet Package Management | Basic | High |
| xUnit Testing | Basic | Medium |
| Git | Basic | Medium |

**Recommended Experience**: 
- 2+ years .NET development
- Familiarity with ASP.NET Core authentication
- Understanding of semantic versioning
- Basic debugging skills

---

### Risk-Adjusted Effort

**Base Estimate**: 4-6 hours

**Risk Adjustments**:
- If IdentityModel changes complex: +2 hours
- If extensive URI issues found: +1 hour
- If EF Core migration issues: +1 hour
- If package conflicts arise: +1 hour

**Worst Case Estimate**: 10 hours (with maximum risk materialization)
**Best Case Estimate**: 3 hours (minimal issues encountered)
**Most Likely Estimate**: 5 hours

---

### Resource Requirements

#### Human Resources
- **Developer**: 1 (full migration)
- **Reviewer** (optional): 1 (code review post-migration)
- **Tester** (optional): 1 (additional QA validation)

#### Tools & Infrastructure
- ✅ Visual Studio 2022 17.12+ or VS Code
- ✅ .NET 10 SDK installed
- ✅ Database instances (PostgreSQL + SQL Server) for testing
- ✅ OAuth2 provider or mock for authentication testing
- ✅ Git for version control

#### Environment
- Development machine with .NET 10 SDK
- Database access (dev/test environments)
- Authentication provider access (if testing real flows)

---

### Complexity Comparison

**Compared to Other Migration Scenarios**:

| Scenario | Complexity | Reason |
|----------|-----------|--------|
| .NET Framework 4.x → .NET 10 | **High-Very High** | Non-SDK projects, massive breaking changes |
| .NET Core 3.1 → .NET 10 | **Medium-High** | Multiple version jumps, many changes |
| .NET 5/6 → .NET 8 | **Low-Medium** | Recent versions, incremental changes |
| **This: .NET 6 → .NET 10** | **Low** | Small jump, modern starting point |
| .NET 8 → .NET 10 | **Very Low** | Minimal changes |

---

### Effort Optimization Tips

**To Minimize Time**:
1. ✅ Use automated package update tools where possible
2. ✅ Leverage compiler errors as a guide (fix in order)
3. ✅ Run tests frequently (fail fast)
4. ✅ Focus on build success before behavioral changes
5. ✅ Use search/replace for repetitive URI test updates

**To Minimize Risk**:
1. ✅ Test authentication thoroughly at each step
2. ✅ Keep rollback branch ready
3. ✅ Commit after each successful phase
4. ✅ Don't skip test runs

---

### Success Probability by Phase

| Phase | Success Probability | Contingency |
|-------|-------------------|-------------|
| Phase 1: Pre-Migration | 99% | Clear prerequisites |
| Phase 2: Core Migration | 90% | Package updates straightforward |
| Phase 3: Test Migration | 95% | Simple updates |
| Phase 4: Code Fixes | 80% | IdentityModel changes uncertain |
| Phase 5: Testing | 85% | Behavioral changes may surprise |
| Phase 6: Finalization | 99% | Cleanup only |
| **Overall** | **85%** | Strong foundation, some unknowns |

---

## Source Control Strategy

### Branch Structure

```
feature/refresh (source branch)
    |
    └── upgrade-to-NET10 (migration branch) ← Work happens here
```

**Migration Branch**: `upgrade-to-NET10`  
**Source Branch**: `feature/refresh`  
**Strategy**: Feature branch workflow

---

### Git Workflow

#### Initial Setup (Already Complete)
```bash
# Starting point
git checkout feature/refresh

# Create migration branch
git checkout -b upgrade-to-NET10
```

#### During Migration

**Commit Strategy**: Commit after each successful phase

##### Phase 1 Commit
```bash
git add .
git commit -m "Phase 1: Pre-migration validation complete

- Verified .NET 10 SDK installed
- Checked global.json compatibility
- Captured baseline test results"
```

##### Phase 2 Commit
```bash
git add IIIFAuth2.API/IIIFAuth2.API.csproj
git commit -m "Phase 2: Migrated IIIFAuth2.API to .NET 10

- Updated target framework to net10.0
- Removed deprecated MediatR.Extensions.Microsoft.DependencyInjection
- Updated Microsoft.* packages to 10.0.3
- Updated IdentityModel packages
- Updated Npgsql.EntityFrameworkCore.PostgreSQL to 10.0.0
- Build successful"
```

##### Phase 3 Commit
```bash
git add IIIFAuth2.API.Tests/IIIFAuth2.API.Tests.csproj
git commit -m "Phase 3: Migrated IIIFAuth2.API.Tests to .NET 10

- Updated target framework to net10.0
- Updated Microsoft.AspNetCore.Mvc.Testing to 10.0.3
- Build successful"
```

##### Phase 4 Commit
```bash
git add -A
git commit -m "Phase 4: Fixed API compatibility issues

- Updated IdentityModel API usage for .NET 10 compatibility
- Fixed HttpContent strategy selection issues
- Addressed URI behavioral changes
- All code compiles without errors"
```

##### Phase 5 Commit
```bash
git add -A
git commit -m "Phase 5: Updated tests for .NET 10 behavioral changes

- Updated URI assertions for new formatting
- Fixed HTTP content type tests
- All tests passing (X passed, Y total)
- Integration testing complete"
```

##### Phase 6 Commit (Final)
```bash
git add .
git commit -m "Phase 6: .NET 10 migration complete

- All projects upgraded from net6.0 to net10.0
- All packages updated
- All tests passing
- Application validated
- Ready for review"
```

---

### Commit Best Practices

1. **Atomic Commits**: One phase = one commit
2. **Descriptive Messages**: Explain what changed and why
3. **Include Outcomes**: Mention build/test status
4. **Reference Issues**: Link to GitHub issues if applicable

---

### Code Review Preparation

Before requesting review:

**Self-Review Checklist**:
- [ ] All commits have clear messages
- [ ] No debug code left in
- [ ] No commented-out code (unless intentional)
- [ ] All tests passing
- [ ] Build warnings reviewed
- [ ] Documentation updated

**PR Description Template**:
```markdown
## .NET 10 Migration

### Summary
Migrated IIIFAuth2 solution from .NET 6.0 to .NET 10.0 LTS.

### Changes
- Updated target frameworks: net6.0 → net10.0
- Updated 10 NuGet packages
- Removed deprecated MediatR.Extensions.Microsoft.DependencyInjection
- Fixed IdentityModel API compatibility issues
- Updated test assertions for URI behavioral changes

### Testing
- ✅ All unit tests passing (X/Y)
- ✅ Integration tests successful
- ✅ Authentication flows validated
- ✅ Database operations confirmed

### Breaking Changes
None - internal migration only

### Deployment Notes
- Requires .NET 10 SDK on deployment target
- No database migration changes
- No configuration changes required

### Review Focus Areas
- IdentityModel API updates (critical for auth)
- URI handling changes
- Test assertion updates
```

---

### Merge Strategy

#### Option 1: Squash and Merge (Recommended for Clean History)
```bash
# After approval
git checkout feature/refresh
git merge --squash upgrade-to-NET10
git commit -m "Upgrade solution to .NET 10.0 LTS"
git branch -d upgrade-to-NET10
```

#### Option 2: Regular Merge (Preserve Detailed History)
```bash
# After approval
git checkout feature/refresh
git merge upgrade-to-NET10 --no-ff
git branch -d upgrade-to-NET10
```

#### Option 3: Rebase and Merge (Linear History)
```bash
# After approval
git checkout upgrade-to-NET10
git rebase feature/refresh
git checkout feature/refresh
git merge upgrade-to-NET10 --ff-only
git branch -d upgrade-to-NET10
```

**Recommendation**: Option 1 (Squash and Merge) for clean history unless detailed phase commits needed.

---

### Rollback Procedures

#### Scenario 1: Issues Found During Migration
```bash
# Abandon current work
git checkout feature/refresh
git branch -D upgrade-to-NET10  # Delete migration branch

# Or reset to last good commit
git checkout upgrade-to-NET10
git reset --hard <last-good-commit-sha>
```

#### Scenario 2: Issues Found After Merge
```bash
# If merged to feature/refresh
git checkout feature/refresh
git revert <merge-commit-sha>

# Or hard reset (if not pushed/shared)
git reset --hard <commit-before-merge>
```

#### Scenario 3: Issues Found in Production
```bash
# Deploy previous version
git checkout feature/refresh
git revert <merge-commit-sha>
# Build and deploy
```

---

### Branch Cleanup

After successful merge and validation:
```bash
# Delete local migration branch
git branch -d upgrade-to-NET10

# If pushed to remote
git push origin --delete upgrade-to-NET10
```

---

### Git Ignore Considerations

Ensure `.gitignore` includes:
```gitignore
# Migration artifacts
.github/upgrades/scenarios/*/
!.github/upgrades/scenarios/*/assessment.md
!.github/upgrades/scenarios/*/plan.md
!.github/upgrades/scenarios/*/tasks.md

# Build artifacts
bin/
obj/
*.user
*.suo

# Test results
TestResults/
*.trx
*.coverage
```

**Note**: Keep assessment.md, plan.md, and tasks.md for documentation

---

## Success Criteria

### Migration Complete When All Criteria Met

#### Build Success ✅
- [ ] Both projects compile without errors
- [ ] Zero blocking warnings (errors treated as warnings)
- [ ] All NuGet packages restored successfully
- [ ] Solution builds in both Debug and Release configurations

**Command**:
```bash
dotnet build --configuration Release
```

**Expected**: `Build succeeded. 0 Warning(s). 0 Error(s).`

---

#### Package Updates ✅
- [ ] All Microsoft.* packages updated to 10.0.3
- [ ] MediatR.Extensions.Microsoft.DependencyInjection removed
- [ ] IdentityModel packages updated to .NET 10 compatible versions
- [ ] Npgsql.EntityFrameworkCore.PostgreSQL updated to 10.0.0
- [ ] No deprecated packages remaining
- [ ] No package version conflicts

**Verification**:
```bash
dotnet list package --outdated
```

**Expected**: No outdated packages for .NET 10 target

---

#### Test Success ✅
- [ ] All unit tests pass
- [ ] Test pass rate >= baseline (captured in Phase 1)
- [ ] No new test failures introduced
- [ ] Code coverage maintained or improved

**Command**:
```bash
dotnet test --configuration Release --logger "console;verbosity=normal"
```

**Expected**: `Test Run Successful. Total tests: X. Passed: X. Failed: 0. Skipped: 0.`

---

#### Application Functionality ✅

##### Startup & Configuration
- [ ] Application starts without exceptions
- [ ] No critical errors in startup logs
- [ ] Swagger UI accessible (if applicable)
- [ ] Health checks pass (if implemented)

##### Authentication & Authorization
- [ ] Token acquisition successful
- [ ] Token validation working
- [ ] Protected endpoints enforce authentication
- [ ] Authorization policies enforced
- [ ] Redirect URIs formatted correctly
- [ ] No OAuth flow errors

##### Data Access
- [ ] Database connection established
- [ ] EF Core queries execute successfully
- [ ] Both PostgreSQL and SQL Server work (if both used)
- [ ] Migrations apply cleanly (if needed)
- [ ] CRUD operations functional

##### API Endpoints
- [ ] GET requests return expected data
- [ ] POST requests create resources
- [ ] PUT requests update resources
- [ ] DELETE requests remove resources
- [ ] Error responses properly formatted
- [ ] Content negotiation working

##### Logging & Diagnostics
- [ ] Serilog writing to configured sinks
- [ ] Log enrichment functional
- [ ] Exception logging captures full details
- [ ] No unexpected errors in logs

---

#### Code Quality ✅
- [ ] No unresolved compiler warnings
- [ ] No TODO comments related to migration
- [ ] No commented-out code blocks
- [ ] Consistent code style maintained
- [ ] API compatibility issues resolved

**Verification**:
```bash
# Check for migration-related TODOs
git grep -i "TODO.*NET10"
git grep -i "TODO.*migration"
```

**Expected**: No unresolved TODOs

---

#### Breaking Changes Addressed ✅
- [ ] All 8 binary incompatibilities resolved (IdentityModel)
- [ ] All 11 source incompatibilities resolved (HttpContent)
- [ ] Behavioral changes reviewed and handled (URI)
- [ ] Test assertions updated for new behaviors
- [ ] No runtime errors from API changes

---

#### Documentation ✅
- [ ] README updated with .NET 10 requirement
- [ ] Build instructions updated
- [ ] Deployment notes updated
- [ ] Migration completed noted in CHANGELOG (if exists)
- [ ] Known issues documented

---

#### Source Control ✅
- [ ] All changes committed
- [ ] Commit messages descriptive
- [ ] No uncommitted files
- [ ] No merge conflicts
- [ ] Branch ready for review/merge

**Verification**:
```bash
git status
```

**Expected**: `nothing to commit, working tree clean`

---

### Go/No-Go Decision Matrix

| Criterion | Weight | Status | Notes |
|-----------|--------|--------|-------|
| **Build Success** | Critical | ⬜ | Must pass |
| **Package Updates** | Critical | ⬜ | Must pass |
| **Test Success** | Critical | ⬜ | Must pass |
| **Authentication Works** | Critical | ⬜ | Must pass |
| **Database Access** | Critical | ⬜ | Must pass |
| **API Endpoints Work** | High | ⬜ | Should pass |
| **Logging Functional** | Medium | ⬜ | Should pass |
| **No Warnings** | Low | ⬜ | Nice to have |
| **Documentation Updated** | Medium | ⬜ | Should complete |

**Decision Rules**:
- **CRITICAL**: All must be ✅ to proceed
- **HIGH**: At least 80% must be ✅
- **MEDIUM**: At least 60% must be ✅
- **LOW**: Informational, can defer

---

### Acceptance Testing Checklist

**Performed By**: [Developer Name]  
**Date**: [Date]  
**Environment**: Development

#### Pre-Migration Baseline
- [ ] Captured test results
- [ ] Documented current behavior
- [ ] Noted any existing issues

#### Post-Migration Validation
- [ ] Build successful
- [ ] Tests passing
- [ ] Application starts
- [ ] Authentication tested
- [ ] Database operations verified
- [ ] API calls successful
- [ ] Logs reviewed
- [ ] No critical errors

#### Sign-Off
```
I confirm that the .NET 10 migration meets all critical success criteria
and is ready for code review and subsequent deployment.

Signature: ___________________
Date: _______________________
```

---

### Deployment Readiness

**Before Deploying to Higher Environments**:
- [ ] All success criteria met in development
- [ ] Code reviewed and approved
- [ ] Tests passing consistently (multiple runs)
- [ ] No open migration-related issues
- [ ] Deployment checklist prepared
- [ ] Rollback plan documented
- [ ] Stakeholders notified

**Deployment Requirements**:
- ✅ .NET 10 SDK installed on target servers
- ✅ No database schema changes (or migrations ready)
- ✅ No configuration changes required (or configs prepared)
- ✅ Monitoring configured for .NET 10 metrics

---

### Success Metrics (Post-Deployment)

**Within 24 Hours**:
- [ ] No critical errors in production logs
- [ ] Authentication success rate maintained
- [ ] API response times within acceptable range
- [ ] No increase in error rates
- [ ] Database query performance acceptable

**Within 1 Week**:
- [ ] Application stability confirmed
- [ ] User-reported issues reviewed
- [ ] Performance metrics normal
- [ ] Team familiar with .NET 10 changes

---

### Definition of Done

**Migration is DONE when**:

1. ✅ All code builds and tests pass
2. ✅ Application functions correctly in all test scenarios
3. ✅ All breaking changes resolved
4. ✅ Documentation updated
5. ✅ Code reviewed and approved
6. ✅ Changes merged to target branch
7. ✅ Deployed successfully (or ready for deployment)
8. ✅ No outstanding critical issues
9. ✅ Stakeholders informed
10. ✅ Knowledge transfer complete (if needed)

---

### Failure Criteria (Rollback Triggers)

**Immediate Rollback If**:
- Authentication completely broken
- Database access fails
- Application won't start
- Critical API endpoints non-functional
- Data corruption risk identified
- Security vulnerability introduced

**Defer and Fix If**:
- Minor test failures (non-critical)
- Performance degradation <10%
- Non-critical warnings
- Documentation gaps
- Minor logging issues

---

**Migration Status**: 🟡 PLANNING COMPLETE - Ready for Execution

**Next Step**: Generate execution tasks from this plan

# Auth0

Custom Auth0 actions used during development.

## AddDlcsClaims

This is a PostLogin script running on Node18, used for development/testing of oidc integration. It will:

* Include `dlcsRole` value from users app_metadata as `https://dlcs.digirati.io:dlcsrole` claim if `dlcs:dlcsrole` scope requested.
* Include `dlcsType` value from users app_metadata as `https://dlcs.digirati.io:dlcstype` claim if `dlcs:dlcstype` scope requested.
* Always include `https://dlcs.digirati.io:designation` claim.
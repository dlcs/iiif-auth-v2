const SCOPE_NAMESPACE = 'dlcs';
const CLAIM_NAMESPACE = 'https://dlcs.digirati.io';

// Claim + Scope for metadata storing direct DLCS role
const roleName = 'dlcsrole';
const roleClaim = `${CLAIM_NAMESPACE}/${roleName}`; // https://dlcs.digirati.io:dlcsrole
const roleScope = `${SCOPE_NAMESPACE}:${roleName}`; // dlcs:dlcsrole

// Claim + Scope for metadata storing DLCS user type
const typeName = 'dlcstype';
const typeClaim = `${CLAIM_NAMESPACE}/${typeName}`; // https://dlcs.digirati.io:dlcstype
const typeScope = `${SCOPE_NAMESPACE}:${typeName}`; // dlcs:dlcstype

const alwaysIncludedClaim = `${CLAIM_NAMESPACE}/designation`; // https://dlcs.digirati.io:designation

const scopeRequested = (event, scope) => { 
    if (!event.transaction || !event.transaction.requested_scopes) return false;
    return !!event.transaction.requested_scopes.includes(scope);
};

/**
* Handler that will be called during the execution of a PostLogin flow.
*
* @param {Event} event - Details about the user and the context in which they are logging in.
* @param {PostLoginAPI} api - Interface whose methods can be used to change the behavior of the login.
*/
exports.onExecutePostLogin = async (event, api) => {
  const metadata = event.user.app_metadata;
    
    // Handle additional scopes that can be requested
    if (metadata){
        const dlcsRole = metadata.dlcsRole;
        if (dlcsRole && scopeRequested(event, roleScope)) {
            api.idToken.setCustomClaim(roleClaim, dlcsRole);
        }

        const dlcsType = metadata.dlcsType;
        if (dlcsType && scopeRequested(event, typeScope)) {
            api.idToken.setCustomClaim(typeClaim, dlcsType);
        }
    }

    // Always add a "dlcs:designation" claim
    api.idToken.setCustomClaim(alwaysIncludedClaim, "tk-421");
};

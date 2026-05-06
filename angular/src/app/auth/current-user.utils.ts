import { ConfigStateService } from '@abp/ng.core';

function normalizeRole(value: unknown): string | null {
  return typeof value === 'string' && value.trim() ? value.trim() : null;
}

function extractRolesFromClaims(claims: unknown): string[] {
  if (!claims) {
    return [];
  }

  if (Array.isArray(claims)) {
    return claims
      .flatMap(claim => {
        if (!claim || typeof claim !== 'object') {
          return [];
        }

        const typedClaim = claim as { type?: unknown; value?: unknown };
        if (
          typedClaim.type === 'role' ||
          typedClaim.type === 'roles' ||
          typedClaim.type === 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
        ) {
          const role = normalizeRole(typedClaim.value);
          return role ? [role] : [];
        }

        return [];
      });
  }

  if (typeof claims === 'object') {
    const record = claims as Record<string, unknown>;
    const claimValues = [
      record.role,
      record.roles,
      record['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
    ];

    return claimValues.flatMap(value => {
      if (Array.isArray(value)) {
        return value.map(normalizeRole).filter((role): role is string => !!role);
      }

      const role = normalizeRole(value);
      return role ? [role] : [];
    });
  }

  return [];
}

export function getCurrentUserRoles(configState: ConfigStateService): string[] {
  const currentUser = (configState.getDeep('currentUser') as Record<string, unknown> | undefined) ?? {};
  const roleSources = [
    currentUser.roles,
    currentUser.roleNames,
  ];

  const directRoles = roleSources.flatMap(value => {
    if (Array.isArray(value)) {
      return value.map(normalizeRole).filter((role): role is string => !!role);
    }

    const role = normalizeRole(value);
    return role ? [role] : [];
  });

  const claimRoles = extractRolesFromClaims(currentUser.claims);

  return [...new Set([...directRoles, ...claimRoles])];
}

export function hasAnyRole(configState: ConfigStateService, roles: string[]): boolean {
  const currentRoles = new Set(getCurrentUserRoles(configState));
  return roles.some(role => currentRoles.has(role));
}

export function hasRole(configState: ConfigStateService, role: string): boolean {
  return hasAnyRole(configState, [role]);
}

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../auth/current-user.utils';

export const studentPortalGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const router = inject(Router);

  if (hasRole(configState, 'Student')) {
    return true;
  }

  return router.createUrlTree(['/']);
};

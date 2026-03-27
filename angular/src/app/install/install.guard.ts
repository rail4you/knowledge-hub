import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';
import { InstallService } from './install.service';

export const installGuard: CanActivateFn = async () => {
  const installService = inject(InstallService);
  const router = inject(Router);
  const oauthService = inject(OAuthService);

  try {
    const status = await installService.getStatus().toPromise();
    
    if (!status?.isInstalled) {
      return true;
    }
    
    router.navigate(['/']);
    return false;
  } catch {
    return true;
  }
};
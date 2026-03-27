import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';
import { InstallService } from './install.service';
import { firstValueFrom } from 'rxjs';

export const installGuard: CanActivateFn = async () => {
  const installService = inject(InstallService);
  const router = inject(Router);

  try {
    const status = await firstValueFrom(installService.getStatus());
    
    if (!status?.isInstalled) {
      return true;
    }
    
    router.navigate(['/']);
    return false;
  } catch {
    return true;
  }
};
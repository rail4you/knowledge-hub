import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';
import { InstallService } from './install.service';
import { firstValueFrom } from 'rxjs';

export function checkInstallStatus() {
  const installService = inject(InstallService);
  const router = inject(Router);

  return async () => {
    try {
      const status = await firstValueFrom(installService.getStatus());
      if (!status.isInstalled && !router.url.startsWith('/install')) {
        router.navigate(['/install']);
      }
    } catch {
      // If API fails, stay on current page
    }
  };
}
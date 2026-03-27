import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';
import { InstallService } from './install.service';
import { firstValueFrom, timeout, retry } from 'rxjs';

export function checkInstallStatus() {
  const installService = inject(InstallService);
  const router = inject(Router);

  return async () => {
    await new Promise(resolve => setTimeout(resolve, 1000));
    
    try {
      const status = await firstValueFrom(
        installService.getStatus().pipe(
          timeout(5000),
          retry({ count: 2, delay: 500 })
        )
      );
      if (!status.isInstalled && !router.url.startsWith('/install')) {
        router.navigate(['/install']);
      }
    } catch {
      // If API fails, stay on current page
    }
  };
}
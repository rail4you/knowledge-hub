import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';
import { InstallService } from './install/install.service';
import { switchMap, tap } from 'rxjs/operators';

export function checkInstallStatus() {
  const installService = inject(InstallService);
  const router = inject(Router);
  const oauthService = inject(OAuthService);

  return () => {
    return installService.getStatus().pipe(
      tap(status => {
        if (!status.isInstalled && !router.url.startsWith('/install')) {
          router.navigate(['/install']);
        }
      })
    ).toPromise();
  };
}
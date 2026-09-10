import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { InstallService } from './install.service';
import { timeout, retry, catchError, of } from 'rxjs';

/**
 * 安装状态检查（非阻塞）。
 *
 * 之前这里 `await sleep(1000)` + 同步等待 `/api/app/install/status`
 *（5s 超时 × 2 次重试），每次冷启动（登录/退出回跳都是整页重载）
 * 都强制白屏至少 1s，弱网下最多阻塞约 16s，是登录卡顿的主因之一。
 *
 * 改为后台触发：不阻塞 bootstrap；仅在确认未安装时才跳 /install。
 * /install 路由本身仍有 installGuard 兜底。
 */
export function checkInstallStatus() {
  const installService = inject(InstallService);
  const router = inject(Router);

  return () => {
    installService
      .getStatus()
      .pipe(
        timeout(5000),
        retry({ count: 2, delay: 500 }),
        catchError(() => of(null))
      )
      .subscribe(status => {
        if (status && !status.isInstalled && !router.url.startsWith('/install')) {
          router.navigate(['/install']);
        }
      });
  };
}
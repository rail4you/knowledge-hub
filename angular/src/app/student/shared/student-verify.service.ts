import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, switchMap } from 'rxjs/operators';
import { NzModalService } from 'ng-zorro-antd/modal';
import { UserService } from '../../proxy/users/user.service';
import type { MyProfileDto } from '../../proxy/users/models';
import { VerifyRealNameModalComponent, type VerifyRealNameModalData } from './verify-real-name-modal.component';

/**
 * 学生端「实名认证」统一入口。
 *
 * <para>
 * 对外开放的共享 / 共建资源库课程用学习闸门要求账号级实名（IsRealNameVerified），
 * 所以学生端任何会触发「选课 / 进入学习」的地方都应该先走这里：
 * 已实名直接放行，未实名弹窗填写，取消则返回 null。
 * </para>
 */
@Injectable({ providedIn: 'root' })
export class StudentVerifyService {
  private readonly userService = inject(UserService);
  private readonly modal = inject(NzModalService);

  /** 打开实名认证弹窗；提交成功返回新资料，取消返回 null。 */
  openVerifyModal(realName?: string | null): Observable<MyProfileDto | null> {
    const ref = this.modal.create<VerifyRealNameModalComponent, VerifyRealNameModalData>({
      nzTitle: '实名认证',
      nzContent: VerifyRealNameModalComponent,
      nzData: { realName },
      nzFooter: null,
      nzWidth: 480,
      nzMaskClosable: false,
    });

    return ref.afterClose as Observable<MyProfileDto | null>;
  }

  /**
   * 确保当前学员已实名：
   * 已实名 → 直接返回资料；未实名 → 弹窗认证，成功返回资料，取消返回 null。
   */
  ensureRealNameVerified(): Observable<MyProfileDto | null> {
    return this.userService.getMyProfile().pipe(
      switchMap(profile => {
        if (profile?.isRealNameVerified) {
          return of(profile);
        }
        return this.openVerifyModal(profile?.realName);
      }),
      catchError(() => of(null)),
    );
  }
}

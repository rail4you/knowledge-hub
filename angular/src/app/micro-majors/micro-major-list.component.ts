import { ChangeDetectionStrategy, Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { MicroMajorDto, MicroMajorService, MicroMajorStatus } from './micro-major.service';
import { hasRole } from '../auth/current-user.utils';

@Component({
  selector: 'app-micro-major-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzInputModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './micro-major-list.component.html',
  styleUrls: ['./micro-major-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MicroMajorListComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly configState = inject(ConfigStateService);

  /** 当前用户是否为 Student 角色（非管理员/教师），控制报名按钮显示 */
  readonly canEnroll = computed(() => hasRole(this.configState, 'Student'));

  readonly loading = signal(false);
  readonly filter = signal('');
  readonly items = signal<MicroMajorDto[]>([]);
  readonly statuses = MicroMajorStatus;

  ngOnInit(): void {
    this.loadItems();
  }

  loadItems(): void {
    this.loading.set(true);
    this.microMajorService.getPublished({
      filter: this.filter() || undefined,
      skipCount: 0,
      maxResultCount: 50,
    }).subscribe({
      next: result => {
        this.items.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  openDetail(id: string): void {
    this.router.navigate(['/micro-majors', id]);
  }

  enroll(item: MicroMajorDto, event: MouseEvent): void {
    event.stopPropagation();
    if (item.isCurrentUserEnrolled) {
      this.message.info('您已报名该微专业');
      return;
    }

    this.microMajorService.enroll(item.id).subscribe({
      next: () => {
        this.message.success('报名成功');
        this.loadItems();
      },
      error: () => {
        this.message.error('报名失败');
      },
    });
  }
}

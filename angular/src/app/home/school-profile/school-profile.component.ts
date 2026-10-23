import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { SchoolService } from '../../proxy/schools/school.service';
import type { SchoolProfileDto } from '../../proxy/schools/dtos/models';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';

/**
 * 学校简介页（公开，游客可访问）。
 *
 * 需求「租户对应的学校是独立实体……在首页上显示学校徽标和名称，点击能跳转到学校简介」。
 * 数据来自 `GET /api/app/school/profile/{tenantId}`，聚合学校档案 + 专业 + 资源库 + 统计。
 */
@Component({
  selector: 'app-school-profile',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    HomeTopNavComponent,
  ],
  templateUrl: './school-profile.component.html',
  styleUrls: ['./school-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SchoolProfileComponent implements OnInit {
  private readonly schoolService = inject(SchoolService);
  private readonly route = inject(ActivatedRoute);

  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set('学校不存在');
      this.loading.set(false);
      return;
    }

    this.schoolService.getProfile(id).subscribe({
      next: (res) => {
        this.profile.set(res);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('学校信息加载失败');
        this.loading.set(false);
      },
    });
  }

  /** 专业维度标签 */
  majorTone(index: number): string {
    return ['sage', 'blue', 'sand', 'amber'][index % 4];
  }
}

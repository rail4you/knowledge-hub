import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';

import { SchoolService } from '../../proxy/schools/school.service';
import type { SchoolProfileDto } from '../../proxy/schools/dtos/models';
import { PortalService } from '../../proxy/portal/portal.service';
import type { PublicCourseDto } from '../../proxy/portal/models';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';

/**
 * 院校独立门户（公开，游客可访问）。
 *
 * <para>对齐原型 <c>#school</c>（campusPortal）：院校头图 → 院校栏目条 → 门户摘要
 * → 本校教学资源库 → 共建共享资源库 → 本校学习与服务。</para>
 *
 * <para>数据来源：<c>GET /api/app/school/profile/{tenantId}</c>（学校档案 + 专业 +
 * 共建库 + 统计）与 <c>GET /api/app/portal/public-browse?tenantId=</c>（本校课程）。</para>
 */
@Component({
  selector: 'app-school-profile',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
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
  private readonly portalService = inject(PortalService);
  private readonly route = inject(ActivatedRoute);

  readonly profile = signal<SchoolProfileDto | null>(null);
  readonly loading = signal<boolean>(true);
  readonly error = signal<string | null>(null);

  /** 本校已发布课程（公开浏览接口按租户过滤） */
  readonly courses = signal<PublicCourseDto[]>([]);

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
        this.loadCourses(id);
      },
      error: () => {
        this.error.set('学校信息加载失败');
        this.loading.set(false);
      },
    });
  }

  /** 拉本校课程（门户「本校教学资源库」板块） */
  private loadCourses(tenantId: string): void {
    this.portalService.getPublicBrowse(tenantId, undefined, undefined, 0, 24).subscribe({
      next: data => this.courses.set(data?.courses || []),
      error: () => this.courses.set([]),
    });
  }

  /** 校徽占位：取校名前两个字 */
  markText(name?: string | null): string {
    const n = (name || '').trim();
    return n ? n.slice(0, 2) : '院校';
  }

  /** 本校学习与服务入口（原型 campusPortal 的 6 个服务格） */
  readonly serviceLinks: { route: string; title: string; desc: string }[] = [
    { route: '/student/practicums', title: '实训中心', desc: '查看本校项目与获授权实践任务' },
    { route: '/explore/micro', title: '微专业', desc: '探索本校开设的培养方向' },
    { route: '/ai/chat', title: 'AI 助手', desc: '基于本人获授权资源辅助学习' },
    { route: '/student/employment/jobs', title: '就业服务', desc: '从课程与实践连接职业发展' },
    { route: '/student', title: '学生工作台', desc: '课程、考核与学习记录' },
    { route: '/admin/workbench', title: '教师工作台', desc: '备课、班级和其他学员管理' },
  ];

  /** 专业维度标签 */
  majorTone(index: number): string {
    return ['sage', 'blue', 'sand', 'amber'][index % 4];
  }

  /** 课程封面色 */
  coverTone(index: number): string {
    return ['green', 'blue', 'orange', 'purple'][index % 4];
  }

  /** 封面大字 */
  coverTitle(title?: string | null): string {
    const t = (title || '').trim();
    return t.length > 8 ? t.slice(0, 8) : t;
  }
}

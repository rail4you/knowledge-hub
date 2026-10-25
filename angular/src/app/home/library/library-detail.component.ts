import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { switchMap } from 'rxjs/operators';

import { PortalService } from '../../proxy/portal/portal.service';
import type {
  PublicResourceLibraryDetailDto,
  PublicLibraryCourseDto,
  PublicLibraryResourceDto,
  PublicLibraryMicroMajorDto,
  PublicLibraryTeacherDto,
  PublicLibraryMemberDto,
} from '../../proxy/portal/models';
import { HomeTopNavComponent } from '../shared/home-top-nav.component';

/**
 * 资源库详情页（原型 <c>shared-library</c> 的公开浏览形态）。
 *
 * <para>
 * 结构：库头横幅（专业 · 多校共建 / 库名 / 主建学校 · 建设负责人 · 建设学校数）
 * → 建设介绍与统计 → 栏目 Tabs（真实数据填充）→ 建设目录侧栏。
 * </para>
 *
 * <para>
 * 原型栏目（课程资源 / 实训资源 / 企业案例 / 标准规范 / 师资发展 / 学生发展 /
 * 建设动态 / 参建单位）里有一部分没有独立数据源，这里按需求「保留样式与布局、
 * 用已有数据填充」的原则映射到真实数据：
 * 课程资源 → 库内课程；实训资源 → 微专业培养路径；企业案例 / 标准规范 → 资源素材；
 * 师资发展 → 库内授课教师；学生发展 → 课程学习人数；建设动态 → 库内资源建设时间线；
 * 参建单位 → 库成员（主建学校 + 参与建设学校）。
 * </para>
 */
@Component({
  selector: 'app-library-detail',
  standalone: true,
  imports: [CommonModule, RouterModule, NzIconModule, NzSpinModule, NzEmptyModule, HomeTopNavComponent],
  templateUrl: './library-detail.component.html',
  styleUrls: ['./library-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LibraryDetailComponent implements OnInit {
  private readonly portal = inject(PortalService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly error = signal('');
  readonly library = signal<PublicResourceLibraryDetailDto | null>(null);

  /** 当前栏目（与原型模板栏目一致） */
  readonly activeTab = signal('课程资源');

  /** 栏目定义：key 为原型栏目名，note 说明该栏目展示的真实数据来源 */
  readonly tabs: { key: string; note: string }[] = [
    { key: '课程资源', note: '库内已发布课程，含授课教师、学时学分与学习人数。' },
    { key: '实训资源', note: '主建学校已发布的微专业培养路径，含课程与配套资源数。' },
    { key: '企业案例', note: '库内已上架的资源素材（企业真实案例、项目任务书等）。' },
    { key: '标准规范', note: '库内已上架的资源素材（专业教学标准、规范与规程类文件）。' },
    { key: '师资发展', note: '库内课程授课教师，按承担课程数排序。' },
    { key: '学生发展', note: '库内课程的学习人数，按课程统计。' },
    { key: '建设动态', note: '库内资源素材的建设时间线。' },
    { key: '参建单位', note: '主建学校与参与建设的学校及其共建角色。' },
  ];

  readonly courses = computed<PublicLibraryCourseDto[]>(() => this.library()?.courses ?? []);
  readonly resources = computed<PublicLibraryResourceDto[]>(() => this.library()?.resources ?? []);
  readonly microMajors = computed<PublicLibraryMicroMajorDto[]>(() => this.library()?.microMajors ?? []);
  readonly teachers = computed<PublicLibraryTeacherDto[]>(() => this.library()?.teachers ?? []);
  readonly members = computed<PublicLibraryMemberDto[]>(() => this.library()?.members ?? []);

  /** 参建单位分为主建学校与参与建设学校 */
  readonly ownerMembers = computed<PublicLibraryMemberDto[]>(() => this.members().filter(m => m.role === 0));
  readonly partnerMembers = computed<PublicLibraryMemberDto[]>(() => this.members().filter(m => m.role !== 0));

  /** 「企业案例」栏目：优先取非视频素材，其余按创建时间倒序 */
  readonly caseResources = computed<PublicLibraryResourceDto[]>(() =>
    this.resources().slice().sort((a, b) => this.extWeight(a) - this.extWeight(b))
  );

  ngOnInit(): void {
    this.route.paramMap
      .pipe(switchMap(pm => this.portal.getPublicLibrary(pm.get('id') || '')))
      .subscribe({
        next: lib => {
          this.library.set(lib);
          this.loading.set(false);
        },
        error: () => {
          this.error.set('资源库不存在或未发布。');
          this.loading.set(false);
        },
      });
  }

  /** 素材类型排序权重：文档/课件优先（更贴近「案例 / 标准规范」） */
  private extWeight(r: PublicLibraryResourceDto): number {
    const e = (r.fileExtension || '').toLowerCase();
    if (e.includes('doc') || e.includes('pdf') || e.includes('ppt') || e.includes('xls')) return 0;
    if (e.includes('mp4') || e.includes('avi') || e.includes('mov')) return 2;
    return 1;
  }

  setTab(key: string): void {
    this.activeTab.set(key);
  }

  /** 当前栏目的条目数 */
  tabCount(): number {
    switch (this.activeTab()) {
      case '课程资源':
      case '学生发展':
        return this.courses().length;
      case '实训资源':
        return this.microMajors().length;
      case '企业案例':
      case '标准规范':
      case '建设动态':
        return this.resources().length;
      case '师资发展':
        return this.teachers().length;
      case '参建单位':
        return this.members().length;
      default:
        return 0;
    }
  }

  /** 当前栏目的数据来源说明（让「用已有数据填充」的映射对用户可见） */
  tabNote(): string {
    return this.tabs.find(t => t.key === this.activeTab())?.note || '';
  }

  /** 资源类型缩写（封面图标里的文字） */
  extLabel(r: PublicLibraryResourceDto): string {
    const e = (r.fileExtension || '').replace(/^\./, '').toUpperCase();
    if (!e) return 'FILE';
    return e.length > 4 ? e.slice(0, 4) : e;
  }

  isVideo(r: PublicLibraryResourceDto): boolean {
    const e = (r.fileExtension || '').toLowerCase();
    return e.includes('mp4') || e.includes('avi') || e.includes('mov') || e.includes('flv') || e.includes('mkv');
  }

  /** 资源类型中文名 */
  typeLabel(r: PublicLibraryResourceDto): string {
    return r.resourceTypeName || '资料';
  }

  /** 建设动态时间格式化 */
  formatDate(value?: string | null): string {
    if (!value) return '—';
    const d = new Date(value);
    if (isNaN(d.getTime())) return '—';
    const pad = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}.${pad(d.getMonth() + 1)}.${pad(d.getDate())}`;
  }

  /** 返回列表页 */
  backToList(): void {
    this.router.navigate(['/explore/library']);
  }
}

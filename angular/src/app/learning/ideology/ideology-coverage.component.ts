import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseContextService } from '../course-context.service';
import { AuthFetchService } from '../../shared/utils/auth-fetch.service';
import { CourseIdeologySettingDto, IdeologyCoverageDto, IdeologyService } from './ideology.service';

interface CourseOption {
  id: string;
  title?: string;
}

/**
 * 覆盖分析：章节覆盖度 / 思政元素分布 / 盲区提示，并导出 xlsx 报告。
 *
 * <p>导出走浏览器原生下载（`AuthFetchService` 带 token），断点续传 / 大文件都不怕。</p>
 */
@Component({
  selector: 'app-ideology-coverage',
  standalone: true,
  imports: [CommonModule, FormsModule, NzCardModule, NzButtonModule, NzSelectModule, NzSwitchModule, NzIconModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="ideology-page">
      <nz-card class="page-header-card" [nzBordered]="false">
        <div class="page-title-block">
          <h1>思政覆盖分析</h1>
          <p>
            按课程的章节与思政元素统计已发布案例的覆盖情况，标出盲区；可导出 xlsx 报告，
            用于课程思政示范课程申报、专业认证与教学评估材料。
          </p>
        </div>
      </nz-card>

      <nz-card [nzBordered]="false">
        <div class="ideology-toolbar">
          <div class="ideology-toolbar-left">
            <nz-select
              class="course-select"
              [ngModel]="courseId()"
              (ngModelChange)="onCourseChange($event)"
              nzPlaceHolder="选择课程"
              nzShowSearch
            >
              @for (course of courses(); track course.id) {
                <nz-option [nzValue]="course.id" [nzLabel]="course.title || course.id"></nz-option>
              }
            </nz-select>
            <button nz-button (click)="reload()" [disabled]="!courseId()">
              <span nz-icon nzType="reload"></span>
              刷新
            </button>
          </div>
          <div class="ideology-toolbar-right">
            <button nz-button (click)="goGraph()" [disabled]="!courseId()">思政图谱</button>
            <button nz-button (click)="goCases()" [disabled]="!courseId()">案例库</button>
            <button nz-button nzType="primary" [nzLoading]="exporting()" [disabled]="!coverage()" (click)="exportXlsx()">
              <span nz-icon nzType="download"></span>
              导出 xlsx
            </button>
          </div>
        </div>

        @if (!courseId()) {
          <div class="empty-tip">请先选择课程。</div>
        } @else if (!coverage()) {
          <div class="empty-tip">加载中…</div>
        } @else if (coverage(); as data) {
          <div class="stat-cards">
            <div class="stat-card">
              <div class="label">案例总数</div>
              <div class="value">{{ data.totalCases }}</div>
            </div>
            <div class="stat-card">
              <div class="label">已发布</div>
              <div class="value">{{ data.publishedCases }}</div>
            </div>
            <div class="stat-card muted">
              <div class="label">草稿</div>
              <div class="value">{{ data.draftCases }}</div>
            </div>
            <div class="stat-card muted">
              <div class="label">已下架</div>
              <div class="value">{{ data.archivedCases }}</div>
            </div>
            <div class="stat-card muted">
              <div class="label">AI 生成</div>
              <div class="value">{{ data.aiGeneratedCases }}</div>
            </div>
          </div>

          @if (data.ideologyGoal) {
            <div class="form-item" style="margin-bottom: 14px">
              <label>课程思政总目标</label>
              <div style="font-size: 13px; color: #183444">{{ data.ideologyGoal }}</div>
            </div>
          }

          <div class="coverage-grid">
            <div>
              <h3 style="margin: 0 0 10px; font-size: 15px">章节覆盖</h3>
              @for (item of data.chapters; track item.id) {
                <div class="coverage-bar">
                  <div class="coverage-bar-head">
                    <span>{{ item.name }}</span>
                    <span>{{ item.caseCount }} 个 · {{ item.percent }}%</span>
                  </div>
                  <div class="coverage-bar-track">
                    <div class="coverage-bar-fill" [style.width.%]="barWidth(item.percent)"></div>
                  </div>
                </div>
              }
              @if (!data.chapters.length) {
                <div class="cell-muted" style="font-size: 13px">这门课还没有章节。</div>
              }
            </div>

            <div>
              <h3 style="margin: 0 0 10px; font-size: 15px">思政元素分布</h3>
              @for (item of data.elements; track item.id) {
                <div class="coverage-bar">
                  <div class="coverage-bar-head">
                    <span>{{ item.name }}</span>
                    <span>{{ item.caseCount }} 个 · {{ item.percent }}%</span>
                  </div>
                  <div class="coverage-bar-track">
                    <div class="coverage-bar-fill" [style.width.%]="barWidth(item.percent)"></div>
                  </div>
                </div>
              }
              @if (!data.elements.length) {
                <div class="cell-muted" style="font-size: 13px">还没有配置思政元素。</div>
              }
            </div>
          </div>

          <div style="margin-top: 16px">
            <h3 style="margin: 0 0 8px; font-size: 15px">待补充</h3>
            @if (data.gaps.length) {
              <ul class="gap-list">
                @for (gap of data.gaps; track gap) {
                  <li>{{ gap }}</li>
                }
              </ul>
            } @else {
              <div class="cell-muted" style="font-size: 13px">章节与元素都已被已发布案例覆盖。</div>
            }
          </div>

          <div class="cell-muted" style="margin-top: 14px; font-size: 12px">
            统计时间：{{ data.generatedAt | date: 'yyyy-MM-dd HH:mm' }}
          </div>
        }

        <div style="margin-top: 14px; border-top: 1px dashed #eef2ef; padding-top: 12px">
          <div class="form-item">
            <label>
              <nz-switch
                [ngModel]="setting()?.isEnabled ?? true"
                (ngModelChange)="toggleEnabled($event)"
                [nzDisabled]="!setting()"
              ></nz-switch>
              <span style="margin-left: 8px">对本课程启用课程思政（关闭后入口隐藏，已有数据保留）</span>
            </label>
          </div>
          <div class="form-item" style="margin-top: 8px">
            <label>课程思政总目标</label>
            <textarea
              nz-input
              rows="2"
              [ngModel]="goal()"
              (ngModelChange)="goal.set($event)"
              placeholder="如：树立法治意识与安全底线思维，理解网络空间是国家主权的延伸"
            ></textarea>
          </div>
          <button nz-button nzType="primary" [nzLoading]="savingSetting()" [disabled]="!setting()" (click)="saveSetting()">
            保存设置
          </button>
        </div>
      </nz-card>
    </div>
  `,
  styleUrls: ['./ideology-pages.scss'],
})
export class IdeologyCoverageComponent implements OnInit {
  private readonly service = inject(IdeologyService);
  private readonly courseService = inject(CourseService);
  private readonly courseContext = inject(CourseContextService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);
  private readonly authFetch = inject(AuthFetchService);

  readonly courses = signal<CourseOption[]>([]);
  readonly courseId = signal<string | null>(null);
  readonly coverage = signal<IdeologyCoverageDto | null>(null);
  readonly setting = signal<CourseIdeologySettingDto | null>(null);
  readonly goal = signal('');
  readonly exporting = signal(false);
  readonly savingSetting = signal(false);

  readonly hasCoverage = computed(() => !!this.coverage());

  ngOnInit(): void {
    const fromQuery = this.route.snapshot.queryParamMap.get('courseId');
    this.loadCourses(fromQuery || this.courseContext.currentCourseId());
  }

  private loadCourses(initial: string | null): void {
    this.courseService.getMyCourses({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: list => {
        this.courses.set((list.items || []).map(c => ({ id: c.id!, title: c.title })));
        const target =
          initial && this.courses().some(c => c.id === initial) ? initial : this.courses()[0]?.id ?? null;
        if (target) {
          this.selectCourse(target);
        }
      },
      error: () => this.courses.set([]),
    });
  }

  selectCourse(courseId: string): void {
    this.courseId.set(courseId);
    this.courseContext.setCourse(courseId);
    this.reload();
  }

  onCourseChange(courseId: string): void {
    this.selectCourse(courseId);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { courseId },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  reload(): void {
    const courseId = this.courseId();
    if (!courseId) return;

    this.coverage.set(null);
    this.service.getCoverage(courseId).subscribe({
      next: data => this.coverage.set(data),
      error: () => this.message.error('读取覆盖分析失败'),
    });

    this.service.getSetting(courseId).subscribe({
      next: data => {
        this.setting.set(data);
        this.goal.set(data.ideologyGoal ?? '');
      },
      error: () => this.setting.set(null),
    });
  }

  barWidth(percent: number): number {
    return Math.max(0, Math.min(100, percent || 0));
  }

  /**
   * 导出 xlsx：走 `AuthFetchService.fetch` 拿到二进制后用 blob 触发下载。
   * （不用 `<a href>` 直链：导出接口要带 Bearer 令牌，直链只会 401。）
   */
  async exportXlsx(): Promise<void> {
    const courseId = this.courseId();
    if (!courseId) return;

    this.exporting.set(true);
    try {
      const response = await this.authFetch.fetch(this.service.getCoverageExportUrl(courseId), {
        method: 'POST',
      });
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}`);
      }

      const blob = await response.blob();
      const fileName = this.resolveFileName(response) ?? `课程思政覆盖分析_${courseId.slice(0, 8)}.xlsx`;
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);

      this.message.success('已开始下载');
    } catch {
      this.message.error('导出失败');
    } finally {
      this.exporting.set(false);
    }
  }

  private resolveFileName(response: Response): string | null {
    const disposition = response.headers.get('content-disposition');
    if (!disposition) return null;
    const match = /filename\*?=(?:UTF-8'')?"?([^;"]+)"?/i.exec(disposition);
    return match ? decodeURIComponent(match[1].trim()) : null;
  }

  toggleEnabled(value: boolean): void {
    const current = this.setting();
    if (!current) return;
    this.setting.set({ ...current, isEnabled: value });
  }

  saveSetting(): void {
    const courseId = this.courseId();
    const current = this.setting();
    if (!courseId || !current) return;

    this.savingSetting.set(true);
    this.service
      .saveSetting({
        courseId,
        isEnabled: current.isEnabled,
        ideologyGoal: this.goal()?.trim() || null,
        note: current.note ?? null,
      })
      .subscribe({
        next: data => {
          this.savingSetting.set(false);
          this.setting.set(data);
          this.message.success('已保存课程思政设置');
        },
        error: () => {
          this.savingSetting.set(false);
          this.message.error('保存失败');
        },
      });
  }

  goCases(): void {
    this.router.navigate(['/learning/ideology-cases'], { queryParams: { courseId: this.courseId() } });
  }

  goGraph(): void {
    this.router.navigate(['/learning/ideology-graph'], { queryParams: { courseId: this.courseId() } });
  }
}

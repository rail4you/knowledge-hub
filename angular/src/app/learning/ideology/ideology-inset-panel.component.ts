import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzPopconfirmModule } from 'ng-zorro-antd/popconfirm';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import {
  CourseIdeologySettingDto,
  IDEOLOGY_POSITION_LABEL,
  IDEOLOGY_USAGE_LABEL,
  IdeologyInsetDto,
  IdeologyInsetPosition,
  IdeologyRecommendItem,
  IdeologyService,
} from './ideology.service';

/**
 * 教案「思政融入」面板（教案页抽屉内容）。
 *
 * <p>三块：课程思政目标 → 推荐思政点（按教案所属章节推荐已发布案例）→ 已插入的融入点。
 * 数据落在 <c>AppIdeologyLessonPlanInsets</c>，**不改教案表**：模块删掉后教案功能毫无影响。</p>
 */
@Component({
  selector: 'app-ideology-inset-panel',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzSelectModule,
    NzTagModule,
    NzIconModule,
    NzTooltipModule,
    NzPopconfirmModule,
    NzEmptyModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="inset-panel">
      @if (loading()) {
        <div class="empty-tip">加载中…</div>
      } @else {
        <div class="inset-section">
          <h4>课程思政目标</h4>
          @if (setting()?.ideologyGoal) {
            <div class="goal-text">{{ setting()!.ideologyGoal }}</div>
          } @else {
            <div class="muted-text">这门课还没设置思政总目标，可在「思政覆盖分析」页底部补充。</div>
          }
        </div>

        <div class="inset-section">
          <h4>推荐思政点</h4>
          <div class="muted-text">依据本教案所属章节推荐的已发布案例（同章节优先，其次课程通用）。</div>
          @if (recommendations().length) {
            @for (item of recommendations(); track item.caseId) {
              <div class="recommend-item">
                <div class="recommend-title">
                  <b>{{ item.title }}</b>
                  <nz-tag nzNoAnimation>{{ usageLabel[item.usageMethod] }}</nz-tag>
                </div>
                <div class="muted-text">{{ item.reason }} · {{ item.elementNames.join('、') || '未标注元素' }}</div>
                @if (item.summary) {
                  <div class="recommend-summary">{{ item.summary }}</div>
                }
                <div class="recommend-actions">
                  <nz-select
                    class="position-select"
                    [ngModel]="positionOf(item.caseId)"
                    (ngModelChange)="setPosition(item.caseId, $event)"
                    nzSize="small"
                  >
                    @for (position of positions; track position) {
                      <nz-option [nzValue]="position" [nzLabel]="positionLabel[position]"></nz-option>
                    }
                  </nz-select>
                  <button nz-button nzType="primary" nzSize="small" (click)="insertRecommend(item)">插入教案</button>
                </div>
              </div>
            }
          } @else {
            <div class="muted-text">没有可推荐的已发布案例（先去「思政案例库」给本课程补几条）。</div>
          }
        </div>

        <div class="inset-section">
          <h4>已插入的思政融入点（{{ insets().length }}）</h4>
          @if (insets().length) {
            @for (inset of insets(); track inset.id) {
              <div class="inset-item">
                <div class="inset-head">
                  <nz-tag nzColor="red">{{ positionLabel[inset.position] }}</nz-tag>
                  <b>{{ inset.title || '自定义思政点' }}</b>
                  <button
                    nz-button
                    nzType="text"
                    nzSize="small"
                    nzDanger
                    nz-popconfirm
                    nzPopconfirmTitle="移出这条思政融入点？"
                    nzOkText="移出"
                    nzCancelText="取消"
                    (nzOnConfirm)="removeInset(inset)"
                  >
                    <span nz-icon nzType="delete"></span>
                  </button>
                </div>
                @if (inset.content) {
                  <div class="inset-content">{{ inset.content }}</div>
                }
                @if (inset.sourceText) {
                  <div class="muted-text">出处：{{ inset.sourceText }}</div>
                }
              </div>
            }
          } @else {
            <nz-empty nzNotFoundImage="simple" nzNotFoundContent="还没有插入思政融入点"></nz-empty>
          }
        </div>

        <div class="inset-section">
          <h4>自定义思政点</h4>
          <textarea
            nz-input
            rows="3"
            [ngModel]="customContent()"
            (ngModelChange)="customContent.set($event)"
            placeholder="不在案例库里的思政点，可直接写在教案这一环节（如：结合本校实训室的能耗管理讲节约意识）"
          ></textarea>
          <div class="custom-actions">
            <nz-select class="position-select" [ngModel]="customPosition()" (ngModelChange)="customPosition.set($event)">
              @for (position of positions; track position) {
                <nz-option [nzValue]="position" [nzLabel]="positionLabel[position]"></nz-option>
              }
            </nz-select>
            <button nz-button nzType="primary" nzSize="small" (click)="insertCustom()">加入教案</button>
          </div>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .inset-panel {
        display: flex;
        flex-direction: column;
        gap: 18px;
      }
      .inset-section h4 {
        margin: 0 0 8px;
        font-size: 14px;
        color: #183444;
      }
      .muted-text {
        font-size: 12px;
        color: #9aa5a2;
        line-height: 1.7;
      }
      .goal-text {
        padding: 10px 12px;
        border-left: 3px solid #a4262c;
        background: #fbe9e7;
        border-radius: 0 8px 8px 0;
        font-size: 13px;
        color: #7a1d17;
        line-height: 1.8;
      }
      .recommend-item,
      .inset-item {
        border: 1px solid #eef2ef;
        border-radius: 8px;
        padding: 10px 12px;
        margin-bottom: 8px;
        background: #fcfdfc;
      }
      .recommend-title,
      .inset-head {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 4px;
        font-size: 13px;
        color: #183444;
      }
      .inset-head b {
        flex: 1 1 auto;
        min-width: 0;
      }
      .recommend-summary,
      .inset-content {
        margin: 6px 0;
        font-size: 12.5px;
        color: #4c5b58;
        line-height: 1.7;
        white-space: pre-wrap;
      }
      .recommend-actions,
      .custom-actions {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-top: 8px;
      }
      .position-select {
        width: 120px;
      }
    `,
  ],
})
export class IdeologyInsetPanelComponent implements OnInit {
  readonly lessonPlanId = input.required<string>();
  readonly courseId = input.required<string>();

  /** 数据变化（插入 / 移出）时通知父组件（用于导出/刷新）。 */
  readonly changed = output<void>();

  private readonly service = inject(IdeologyService);
  private readonly message = inject(NzMessageService);

  readonly positionLabel = IDEOLOGY_POSITION_LABEL;
  readonly usageLabel = IDEOLOGY_USAGE_LABEL;
  readonly positions = [
    IdeologyInsetPosition.Import,
    IdeologyInsetPosition.Lecture,
    IdeologyInsetPosition.Practice,
    IdeologyInsetPosition.Summary,
    IdeologyInsetPosition.Homework,
  ];

  readonly loading = signal(true);
  readonly insets = signal<IdeologyInsetDto[]>([]);
  readonly recommendations = signal<IdeologyRecommendItem[]>([]);
  readonly setting = signal<CourseIdeologySettingDto | null>(null);
  readonly customContent = signal('');
  readonly customPosition = signal<IdeologyInsetPosition>(IdeologyInsetPosition.Import);
  private readonly positionsByCase = signal<Record<string, IdeologyInsetPosition>>({});

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.loading.set(true);
    this.service.getInsets(this.lessonPlanId()).subscribe({
      next: list => this.insets.set(list || []),
      error: () => this.insets.set([]),
    });

    this.service.getRecommend(this.lessonPlanId()).subscribe({
      next: list => {
        this.recommendations.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.recommendations.set([]);
        this.loading.set(false);
      },
    });

    this.service.getSetting(this.courseId()).subscribe({
      next: data => this.setting.set(data),
      error: () => this.setting.set(null),
    });
  }

  positionOf(caseId: string): IdeologyInsetPosition {
    return this.positionsByCase()[caseId] ?? IdeologyInsetPosition.Import;
  }

  setPosition(caseId: string, position: IdeologyInsetPosition): void {
    this.positionsByCase.set({ ...this.positionsByCase(), [caseId]: position });
  }

  insertRecommend(item: IdeologyRecommendItem): void {
    this.service
      .createInset({
        lessonPlanId: this.lessonPlanId(),
        caseId: item.caseId,
        position: this.positionOf(item.caseId),
      })
      .subscribe({
        next: () => {
          this.message.success('已插入教案');
          this.reload();
          this.changed.emit();
        },
        error: (err: any) => this.message.error(err?.error?.error?.message || '插入失败'),
      });
  }

  insertCustom(): void {
    const content = this.customContent().trim();
    if (!content) {
      this.message.warning('请先填写自定义思政点内容');
      return;
    }

    this.service
      .createInset({
        lessonPlanId: this.lessonPlanId(),
        position: this.customPosition(),
        title: content.length > 30 ? content.slice(0, 30) + '…' : content,
        content,
      })
      .subscribe({
        next: () => {
          this.customContent.set('');
          this.message.success('已加入教案');
          this.reload();
          this.changed.emit();
        },
        error: (err: any) => this.message.error(err?.error?.error?.message || '保存失败'),
      });
  }

  removeInset(inset: IdeologyInsetDto): void {
    this.service.deleteInset(inset.id).subscribe({
      next: () => {
        this.message.success('已移出');
        this.reload();
        this.changed.emit();
      },
      error: () => this.message.error('移出失败'),
    });
  }

  readonly hasData = computed(() => this.insets().length > 0);
}

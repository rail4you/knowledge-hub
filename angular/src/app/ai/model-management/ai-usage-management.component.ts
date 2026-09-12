import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzDescriptionsModule } from 'ng-zorro-antd/descriptions';
import { Subject, takeUntil } from 'rxjs';
import {
  AiManagementService,
  type AiManagementStatusDto,
} from '../services/ai-management.service';

interface QuotaRow {
  role: string;
  values: Record<string, string>;
}

/**
 * 租户级 AI 配置：Qwen API Key（租户各自分配）+ 每日配额。
 * 只影响当前租户；平台（host）不在此维护配置。
 */
@Component({
  selector: 'app-ai-usage-management',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    NzCardModule, NzButtonModule, NzInputModule, NzTableModule,
    NzTagModule, NzDescriptionsModule,
  ],
  templateUrl: './ai-usage-management.component.html',
  styleUrls: ['./ai-usage-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiUsageManagementComponent implements OnInit {
  private readonly api = inject(AiManagementService);
  private readonly message = inject(NzMessageService);
  private readonly destroy$ = new Subject<void>();

  // ============= Key 与模型 =============
  readonly status = signal<AiManagementStatusDto | null>(null);
  readonly statusLoading = signal(false);
  readonly newApiKey = signal('');
  readonly savingKey = signal(false);
  readonly testing = signal(false);

  readonly featureGroups = [
    { value: 'CareerGuidance', label: '职业规划生成' },
    { value: 'LessonPlan', label: '教案生成' },
    { value: 'CaseAnalysis', label: '案例分析' },
    { value: 'ExerciseGenerate', label: '习题生成' },
    { value: 'Chat', label: 'AI 对话' },
    { value: 'Video', label: '视频理解' },
    { value: 'Summary', label: '文档摘要' },
  ];

  // ============= 配额 =============
  readonly quotaGroups = ['CareerGuidance', 'Chat', 'LessonPlan', 'CaseAnalysis', 'ExerciseGenerate'];
  readonly quotaRoles = ['Student', 'Teacher', 'SchoolAdmin', 'admin'];
  readonly quotaRows = signal<QuotaRow[]>([]);
  readonly quotasLoading = signal(false);
  readonly savingQuotas = signal(false);

  ngOnInit(): void {
    this.loadStatus();
    this.loadQuotas();
  }

  groupLabel(value: string): string {
    return this.featureGroups.find(g => g.value === value)?.label || value;
  }

  // ---------- Key ----------
  loadStatus(): void {
    this.statusLoading.set(true);
    this.api.getStatus()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: s => {
          this.status.set(s);
          this.statusLoading.set(false);
        },
        error: () => {
          this.statusLoading.set(false);
          this.message.error('加载 AI 服务状态失败（需要 AI 管理权限）');
        },
      });
  }

  saveApiKey(): void {
    const key = this.newApiKey().trim();
    if (!key) {
      this.message.warning('请先输入新的 API Key');
      return;
    }
    this.savingKey.set(true);
    this.api.updateApiKey(key)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.savingKey.set(false);
          this.newApiKey.set('');
          this.message.success('本租户 API Key 已更新，新调用即时生效，无需重启');
          this.loadStatus();
        },
        error: err => {
          this.savingKey.set(false);
          this.message.error('保存失败：' + (err?.error?.error?.message || '未知错误'));
        },
      });
  }

  testConnection(): void {
    this.testing.set(true);
    this.api.testConnection()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: ok => {
          this.testing.set(false);
          if (ok) this.message.success('连接正常，API Key 有效');
          else this.message.error('连接失败，请检查 Key 是否正确或是否欠费');
        },
        error: () => {
          this.testing.set(false);
          this.message.error('连接失败，请检查 Key 是否正确或是否欠费');
        },
      });
  }

  // ---------- 配额 ----------
  loadQuotas(): void {
    this.quotasLoading.set(true);
    this.api.getQuotas()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: res => {
          const quotas = res.quotas || {};
          const roles = Array.from(new Set([...this.quotaRoles, ...Object.keys(quotas)]));
          this.quotaRows.set(roles.map(role => ({
            role,
            values: Object.fromEntries(
              this.quotaGroups.map(g => [g, quotas[role]?.[g] == null ? '' : String(quotas[role][g])])),
          })));
          this.quotasLoading.set(false);
        },
        error: () => this.quotasLoading.set(false),
      });
  }

  saveQuotas(): void {
    const quotas: Record<string, Record<string, number | null>> = {};
    for (const row of this.quotaRows()) {
      quotas[row.role] = {};
      for (const g of this.quotaGroups) {
        const raw = (row.values[g] || '').trim();
        if (!raw) {
          quotas[row.role][g] = null;
          continue;
        }
        const n = Number(raw);
        if (!Number.isInteger(n) || n < 0) {
          this.message.warning(`配额格式错误：${row.role} / ${this.groupLabel(g)}（填空=不限，非负整数）`);
          return;
        }
        quotas[row.role][g] = n;
      }
    }
    this.savingQuotas.set(true);
    this.api.updateQuotas({ quotas })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.savingQuotas.set(false);
          this.message.success('本租户配额已更新，即时生效');
        },
        error: err => {
          this.savingQuotas.set(false);
          this.message.error('保存失败：' + (err?.error?.error?.message || '未知错误'));
        },
      });
  }
}

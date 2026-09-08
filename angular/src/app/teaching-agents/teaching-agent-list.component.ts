import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TeachingAgentService } from './teaching-agent.service';
import {
  CreateUpdateTeachingAgentPayload,
  DEFAULT_SKILL_CATALOG,
  FIXED_TEACHING_AGENT_MODEL,
  TEACHING_AGENT_VISIBILITY,
  TeachingAgent,
  TeachingAgentDetail,
  TeachingAgentPreset,
  agentStatusLabel,
  visibilityLabel,
} from './models';

@Component({
  selector: 'app-teaching-agent-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './teaching-agent-list.component.html',
  styleUrls: ['./teaching-agent-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingAgentListComponent implements OnInit {
  private readonly teachingAgentService = inject(TeachingAgentService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly loading = signal(false);
  readonly agents = signal<TeachingAgent[]>([]);
  readonly filter = signal('');

  // 表格分页（前端分页：数据已全量加载，按页切片展示）
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);
  readonly pagedAgents = computed(() => {
    const all = this.agents();
    const start = (this.pageIndex() - 1) * this.pageSize();
    return all.slice(start, start + this.pageSize());
  });

  // ─── Create modal state ───
  readonly createModalVisible = signal(false);
  readonly creating = signal(false);
  readonly presetsLoading = signal(false);
  readonly presets = signal<TeachingAgentPreset[]>([]);
  readonly selectedPresetCode = signal<string | null>(null);
  readonly publishNote = signal('');
  readonly form = signal<CreateUpdateTeachingAgentPayload>(this.emptyForm());

  readonly selectedPresetName = computed(() => {
    const code = this.selectedPresetCode();
    if (!code) return '';
    return this.presets().find(p => p.code === code)?.name ?? '';
  });

  // ─── Edit modal state ───
  readonly editModalVisible = signal(false);
  readonly editLoading = signal(false);
  readonly updating = signal(false);
  readonly editingAgent = signal<TeachingAgentDetail | null>(null);

  // ─── Publish modal state ───
  readonly publishModalVisible = signal(false);
  readonly publishing = signal(false);
  readonly publishingAgent = signal<TeachingAgent | null>(null);

  ngOnInit(): void {
    void this.reload();
  }

  reload(): void {
    this.pageIndex.set(1);
    void this.loadAgents();
  }

  resetSearch(): void {
    this.filter.set('');
    this.reload();
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.pageIndex.set(1);
  }

  private async loadAgents(): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.teachingAgentService.getList({
        filter: this.filter().trim() || undefined,
        skipCount: 0,
        maxResultCount: 100,
      }).toPromise();

      this.agents.set(result?.items ?? []);
    } finally {
      this.loading.set(false);
    }
  }

  trackById(_: number, item: { id: string }): string {
    return item.id;
  }

  statusLabel(status: number): string {
    return agentStatusLabel(status);
  }

  visibilityText(visibility: number): string {
    return visibilityLabel(visibility);
  }

  // ─── Create modal ───
  async openCreateModal(): Promise<void> {
    this.createModalVisible.set(true);
    this.selectedPresetCode.set(null);
    this.publishNote.set('');
    this.form.set(this.emptyForm());

    if (this.presets().length === 0) {
      this.presetsLoading.set(true);
      try {
        const presets = await this.teachingAgentService.getPresets().toPromise();
        this.presets.set(presets ?? []);
      } finally {
        this.presetsLoading.set(false);
      }
    }
  }

  closeCreateModal(): void {
    if (this.creating()) return;
    this.createModalVisible.set(false);
  }

  selectPreset(code: string): void {
    const preset = this.presets().find(p => p.code === code);
    if (!preset) return;

    this.selectedPresetCode.set(code);
    this.form.update(current => ({
      ...current,
      name: current.name || `${preset.name}`,
      description: preset.description,
      systemPrompt: preset.systemPrompt,
      welcomeMessage: preset.welcomeMessage || '',
    }));
  }

  setFormField<K extends keyof CreateUpdateTeachingAgentPayload>(
    key: K,
    value: CreateUpdateTeachingAgentPayload[K],
  ): void {
    this.form.update(current => ({ ...current, [key]: value }));
  }

  async confirmCreate(): Promise<void> {
    const payload = this.form();
    if (!payload.name.trim()) {
      this.message.error('请填写智能体名称');
      return;
    }
    if (!payload.systemPrompt.trim()) {
      this.message.error('请填写系统提示词');
      return;
    }

    this.creating.set(true);
    try {
      const saved = await this.teachingAgentService.create(payload).toPromise();
      if (saved?.id) {
        this.message.success('智能体已创建');
        this.createModalVisible.set(false);
        await this.loadAgents();
      }
    } catch (err: any) {
      const detail = err?.error?.error?.message || err?.error?.message || err?.message || '创建失败';
      this.message.error(detail);
    } finally {
      this.creating.set(false);
    }
  }

  // ─── Edit modal ───
  async openEditModal(agent: TeachingAgent): Promise<void> {
    this.editModalVisible.set(true);
    this.editingAgent.set(null);
    this.publishNote.set('');
    this.form.set(this.emptyForm());
    this.editLoading.set(true);

    try {
      const detail = await this.teachingAgentService.get(agent.id).toPromise();
      if (!detail) {
        this.message.error('未找到该智能体');
        this.editModalVisible.set(false);
        return;
      }

      this.editingAgent.set(detail);
      const version = detail.draftVersion ?? detail.publishedVersion;
      this.form.set({
        name: detail.name ?? '',
        description: detail.description ?? '',
        visibility: detail.visibility ?? TEACHING_AGENT_VISIBILITY.private,
        systemPrompt: version?.systemPrompt ?? '',
        welcomeMessage: version?.welcomeMessage ?? '',
        modelId: FIXED_TEACHING_AGENT_MODEL,
        temperature: 0.2,
        versionNote: version?.versionNote ?? '',
        skills: (version?.skills ?? []).length > 0
          ? version!.skills
          : DEFAULT_SKILL_CATALOG.map(skill => ({ ...skill })),
      });
      this.publishNote.set(version?.versionNote ?? '');
    } catch (err: any) {
      const detail2 = err?.error?.error?.message || err?.error?.message || err?.message || '加载失败';
      this.message.error(detail2);
      this.editModalVisible.set(false);
    } finally {
      this.editLoading.set(false);
    }
  }

  closeEditModal(): void {
    if (this.updating()) return;
    this.editModalVisible.set(false);
  }

  async confirmUpdate(): Promise<void> {
    const agent = this.editingAgent();
    if (!agent) return;

    const payload = this.form();
    if (!payload.name.trim()) {
      this.message.error('请填写智能体名称');
      return;
    }
    if (!payload.systemPrompt.trim()) {
      this.message.error('请填写系统提示词');
      return;
    }

    this.updating.set(true);
    try {
      await this.teachingAgentService.update(agent.id, payload).toPromise();
      this.message.success('已保存为草稿');
      this.editModalVisible.set(false);
      await this.loadAgents();
    } catch (err: any) {
      const detail = err?.error?.error?.message || err?.error?.message || err?.message || '保存失败';
      this.message.error(detail);
    } finally {
      this.updating.set(false);
    }
  }

  // ─── Publish modal ───
  openPublishModal(agent: TeachingAgent): void {
    this.publishingAgent.set(agent);
    this.publishNote.set('');
    this.publishModalVisible.set(true);
  }

  closePublishModal(): void {
    if (this.publishing()) return;
    this.publishModalVisible.set(false);
  }

  async confirmPublish(): Promise<void> {
    const agent = this.publishingAgent();
    if (!agent) return;

    this.publishing.set(true);
    try {
      await this.teachingAgentService.publish(agent.id, this.publishNote()).toPromise();
      this.message.success(`${agent.name} 已发布`);
      this.publishModalVisible.set(false);
      await this.loadAgents();
    } catch (err: any) {
      const detail = err?.error?.error?.message || err?.error?.message || err?.message || '发布失败';
      this.message.error(detail);
    } finally {
      this.publishing.set(false);
    }
  }

  // ─── Unpublish (published → draft) ───
  async unpublishAgent(agent: TeachingAgent, closeEditAfter = false): Promise<void> {
    this.modal.confirm({
      nzTitle: '下架智能体',
      nzContent: `确认将「${agent.name}」下架为草稿？下架后学生端将无法再使用该智能体。`,
      nzOkText: '确认下架',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: async () => {
        try {
          await this.teachingAgentService.unpublish(agent.id).toPromise();
          this.message.success(`「${agent.name}」已下架为草稿`);
          if (closeEditAfter) {
            this.editModalVisible.set(false);
          }
          await this.loadAgents();
        } catch (err: any) {
          const detail = err?.error?.error?.message || err?.error?.message || err?.message || '下架失败';
          this.message.error(detail);
        }
      },
    });
  }

  private emptyForm(): CreateUpdateTeachingAgentPayload {
    return {
      name: '',
      description: '',
      visibility: TEACHING_AGENT_VISIBILITY.private,
      systemPrompt: '',
      welcomeMessage: '',
      modelId: FIXED_TEACHING_AGENT_MODEL,
      temperature: 0.2,
      versionNote: '',
      skills: DEFAULT_SKILL_CATALOG.map(skill => ({ ...skill })),
    };
  }
}
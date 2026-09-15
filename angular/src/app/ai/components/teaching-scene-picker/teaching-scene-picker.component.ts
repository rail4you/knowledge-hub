import { Component, inject, input, output, signal, computed, effect, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzMessageService } from 'ng-zorro-antd/message';
import {
  TeachingScene,
  TeachingSceneCategory,
  TeachingSceneService,
} from '../../services/teaching-scene.service';

/**
 * 教学场景选择器：表格展示「我的场景」（使用 / 编辑 / 删除）。
 * 内置模板从表格中移出，在「添加场景」表单的「选择模板」下拉中选取并自动填充。
 */
@Component({
  selector: 'app-teaching-scene-picker',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
  ],
  templateUrl: './teaching-scene-picker.component.html',
  styleUrls: ['./teaching-scene-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingScenePickerComponent {
  private readonly sceneService = inject(TeachingSceneService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);

  readonly category = input.required<TeachingSceneCategory>();
  readonly title = input('教学场景示例（点击填入）');
  readonly promptSelected = output<string>();
  /** 携带场景对象的选择事件（供上层同时获得场景名称与提示词）。 */
  readonly sceneSelected = output<TeachingScene>();

  readonly scenes = signal<TeachingScene[]>([]);
  readonly loading = signal(false);

  /** 表格只展示「我的场景」；内置模板放到新建表单的「选择模板」下拉里。 */
  readonly tableScenes = computed(() => this.scenes().filter(s => !s.isSystem));
  readonly templateOptions = computed(() => this.scenes().filter(s => s.isSystem));

  readonly modalVisible = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly templateId = signal<string | null>(null);
  readonly formName = signal('');
  readonly formPrompt = signal('');
  readonly saving = signal(false);

  constructor() {
    effect(() => {
      const category = this.category();
      this.load(category);
    });
  }

  private load(category: TeachingSceneCategory) {
    this.loading.set(true);
    this.sceneService.getList(category).subscribe({
      next: scenes => {
        this.scenes.set(scenes ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.scenes.set([]);
        this.loading.set(false);
      },
    });
  }

  select(scene: TeachingScene) {
    this.promptSelected.emit(scene.prompt);
    this.sceneSelected.emit(scene);
  }

  openAdd() {
    this.editingId.set(null);
    this.templateId.set(null);
    this.formName.set('');
    this.formPrompt.set('');
    this.modalVisible.set(true);
  }

  /** 选择内置模板：自动填充名称与提示词，可在此基础上修改。 */
  onTemplateChange(id: string | null) {
    this.templateId.set(id);
    if (!id) return;
    const tpl = this.templateOptions().find(t => t.id === id);
    if (tpl) {
      this.formName.set(tpl.name);
      this.formPrompt.set(tpl.prompt);
    }
  }

  openEdit(scene: TeachingScene) {
    this.editingId.set(scene.id);
    this.templateId.set(null);
    this.formName.set(scene.name);
    this.formPrompt.set(scene.prompt);
    this.modalVisible.set(true);
  }

  closeModal() {
    this.modalVisible.set(false);
  }

  save() {
    const name = this.formName().trim();
    const prompt = this.formPrompt().trim();
    if (!name) {
      this.message.warning('请输入场景名称');
      return;
    }
    if (!prompt) {
      this.message.warning('请输入提示词');
      return;
    }

    this.saving.set(true);
    const payload = { name, prompt, category: this.category(), sortOrder: 0 };
    const id = this.editingId();
    const request = id ? this.sceneService.update(id, payload) : this.sceneService.create(payload);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.modalVisible.set(false);
        this.message.success(id ? '场景已更新' : '场景已添加');
        this.load(this.category());
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || '保存失败');
      },
    });
  }

  remove(scene: TeachingScene) {
    this.modal.confirm({
      nzTitle: '删除场景',
      nzContent: `确定删除「${scene.name}」吗？`,
      nzOkText: '删除',
      nzOkDanger: true,
      nzOnOk: () =>
        new Promise<void>((resolve, reject) => {
          this.sceneService.delete(scene.id).subscribe({
            next: () => {
              this.message.success('已删除');
              this.load(this.category());
              resolve();
            },
            error: () => {
              this.message.error('删除失败');
              reject();
            },
          });
        }),
    });
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TeachingAgentService } from './teaching-agent.service';
import {
  CreateUpdateTeachingAgentPayload,
  DEFAULT_SKILL_CATALOG,
  FIXED_TEACHING_AGENT_MODEL,
  TEACHING_AGENT_VISIBILITY,
  TeachingAgentDetail,
  TeachingAgentPreset,
  TeachingAgentSkillBinding,
  TeachingAgentVersion,
  agentStatusLabel,
  visibilityLabel,
} from './models';

@Component({
  selector: 'app-teaching-agent-editor',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzInputNumberModule,
    NzSelectModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './teaching-agent-editor.component.html',
  styleUrls: ['./teaching-agent-editor.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingAgentEditorComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly teachingAgentService = inject(TeachingAgentService);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly presets = signal<TeachingAgentPreset[]>([]);
  readonly currentAgent = signal<TeachingAgentDetail | null>(null);
  readonly publishNote = signal('');
  readonly skillCatalog = signal<TeachingAgentSkillBinding[]>(DEFAULT_SKILL_CATALOG);
  readonly form = signal<CreateUpdateTeachingAgentPayload>({
    name: '',
    description: '',
    visibility: TEACHING_AGENT_VISIBILITY.private,
    systemPrompt: '',
    welcomeMessage: '',
    modelId: FIXED_TEACHING_AGENT_MODEL,
    temperature: 0.2,
    versionNote: '',
    skills: DEFAULT_SKILL_CATALOG.map(skill => ({ ...skill })),
  });
  readonly runtimeModel = FIXED_TEACHING_AGENT_MODEL;

  readonly isEditMode = computed(() => !!this.currentAgent()?.id);

  async ngOnInit(): Promise<void> {
    this.loading.set(true);
    try {
      const presets = await this.teachingAgentService.getPresets().toPromise();
      this.presets.set(presets ?? []);

      const agentId = this.route.snapshot.paramMap.get('id');
      if (agentId) {
        const agent = await this.teachingAgentService.get(agentId).toPromise();
        if (agent) {
          this.currentAgent.set(agent);
          this.form.set(this.mapVersionToForm(agent.draftVersion ?? agent.publishedVersion));
          this.publishNote.set(agent.draftVersion?.versionNote ?? '');
          this.refreshSkillCatalog(this.form().skills);
        }
      } else {
        const presetCode = this.route.snapshot.queryParamMap.get('preset');
        if (presetCode) {
          this.applyPreset(presetCode);
        }
      }
    } finally {
      this.loading.set(false);
    }
  }

  setField<K extends keyof CreateUpdateTeachingAgentPayload>(key: K, value: CreateUpdateTeachingAgentPayload[K]): void {
    this.form.update(current => ({ ...current, [key]: value }));
  }

  toggleSkill(code: string, enabled: boolean): void {
    const nextSkills = this.skillCatalog().map(skill =>
      skill.code === code ? { ...skill, enabled } : { ...skill }
    );

    this.skillCatalog.set(nextSkills);
    this.form.update(current => ({ ...current, skills: nextSkills.map(skill => ({ ...skill })) }));
  }

  applyPreset(code: string): void {
    const preset = this.presets().find(item => item.code === code);
    if (!preset) {
      return;
    }

    const mergedSkills = this.mergeSkills(preset.skills);
    this.skillCatalog.set(mergedSkills);
    this.form.update(current => ({
      ...current,
      name: current.name || `${preset.name} - ${new Date().toLocaleDateString('zh-CN')}`,
      description: preset.description,
      systemPrompt: preset.systemPrompt,
      welcomeMessage: preset.welcomeMessage || '',
      skills: mergedSkills,
    }));
  }

  async saveDraft(): Promise<void> {
    this.saving.set(true);
    try {
      const agent = this.currentAgent();
      const payload = this.form();
      const saved = agent
        ? await this.teachingAgentService.update(agent.id, payload).toPromise()
        : await this.teachingAgentService.create(payload).toPromise();

      if (saved?.id) {
        await this.router.navigate(['/teaching/agents', saved.id]);
      }
    } finally {
      this.saving.set(false);
    }
  }

  async publish(): Promise<void> {
    const agent = this.currentAgent();
    if (!agent) {
      await this.saveDraft();
      return;
    }

    this.saving.set(true);
    try {
      await this.teachingAgentService.publish(agent.id, this.publishNote()).toPromise();
      const refreshed = await this.teachingAgentService.get(agent.id).toPromise();
      if (refreshed) {
        this.currentAgent.set(refreshed);
      }
    } finally {
      this.saving.set(false);
    }
  }

  visibilityText(value: number): string {
    return visibilityLabel(value);
  }

  statusText(value: number): string {
    return agentStatusLabel(value);
  }

  private mapVersionToForm(version?: TeachingAgentVersion): CreateUpdateTeachingAgentPayload {
    const agent = this.currentAgent();
    const mergedSkills = this.mergeSkills(version?.skills ?? []);

    return {
      name: agent?.name ?? '',
      description: agent?.description ?? '',
      visibility: agent?.visibility ?? TEACHING_AGENT_VISIBILITY.private,
      systemPrompt: version?.systemPrompt ?? '',
      welcomeMessage: version?.welcomeMessage ?? '',
      modelId: version?.modelId ?? FIXED_TEACHING_AGENT_MODEL,
      temperature: version?.temperature ?? 0.2,
      versionNote: version?.versionNote ?? '',
      skills: mergedSkills,
    };
  }

  private refreshSkillCatalog(skills: TeachingAgentSkillBinding[]): void {
    this.skillCatalog.set(this.mergeSkills(skills));
    this.form.update(current => ({
      ...current,
      skills: this.mergeSkills(current.skills),
    }));
  }

  private mergeSkills(skills: TeachingAgentSkillBinding[]): TeachingAgentSkillBinding[] {
    const catalog = new Map(DEFAULT_SKILL_CATALOG.map(skill => [skill.code, { ...skill }]));
    for (const skill of skills) {
      catalog.set(skill.code, { ...catalog.get(skill.code), ...skill });
    }
    return Array.from(catalog.values());
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { TeachingAgentService } from './teaching-agent.service';
import { TeachingAgent, TeachingAgentPreset, TeachingAgentSkillBinding, agentStatusLabel, visibilityLabel } from './models';

@Component({
  selector: 'app-teaching-agent-list',
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
    NzSpinModule,
    NzTagModule,
    NzTableModule,
    NzTabsModule,
  ],
  templateUrl: './teaching-agent-list.component.html',
  styleUrls: ['./teaching-agent-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingAgentListComponent implements OnInit {
  private readonly teachingAgentService = inject(TeachingAgentService);

  private readonly search$ = new Subject<string>();

  readonly loading = signal(false);
  readonly agents = signal<TeachingAgent[]>([]);
  readonly presets = signal<TeachingAgentPreset[]>([]);
  readonly filter = signal('');
  activeTabIndex = 0;

  /** Locally-filtered presets based on search text */
  readonly filteredPresets = computed(() => {
    const keyword = this.filter().trim().toLowerCase();
    const all = this.presets();
    if (!keyword) return all;
    return all.filter(p =>
      p.name.toLowerCase().includes(keyword) ||
      p.description.toLowerCase().includes(keyword) ||
      p.skills.some(s => s.name.toLowerCase().includes(keyword)),
    );
  });

  ngOnInit(): void {
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe(() => { void this.loadAgents(); });

    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [presets, result] = await Promise.all([
        this.teachingAgentService.getPresets().toPromise(),
        this.teachingAgentService.getList({
          filter: this.filter().trim() || undefined,
          skipCount: 0,
          maxResultCount: 24,
        }).toPromise(),
      ]);

      this.presets.set(presets ?? []);
      this.agents.set(result?.items ?? []);
    } finally {
      this.loading.set(false);
    }
  }

  async loadAgents(): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.teachingAgentService.getList({
        filter: this.filter().trim() || undefined,
        skipCount: 0,
        maxResultCount: 24,
      }).toPromise();

      this.agents.set(result?.items ?? []);
    } finally {
      this.loading.set(false);
    }
  }

  onSearchInput(value: string): void {
    this.filter.set(value);
    this.search$.next(value);
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

  enabledSkills(agent: TeachingAgent): TeachingAgentSkillBinding[] {
    return (agent.draftVersion?.skills || []).filter(s => s.enabled);
  }
}

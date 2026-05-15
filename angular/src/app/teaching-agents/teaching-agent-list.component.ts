import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TeachingAgentService } from './teaching-agent.service';
import { TeachingAgent, TeachingAgentPreset, agentStatusLabel, visibilityLabel } from './models';

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
  ],
  templateUrl: './teaching-agent-list.component.html',
  styleUrls: ['./teaching-agent-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingAgentListComponent implements OnInit {
  private readonly teachingAgentService = inject(TeachingAgentService);

  readonly loading = signal(false);
  readonly agents = signal<TeachingAgent[]>([]);
  readonly presets = signal<TeachingAgentPreset[]>([]);
  readonly filter = signal('');

  ngOnInit(): void {
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

  trackById(_: number, item: { id: string }): string {
    return item.id;
  }

  statusLabel(status: number): string {
    return agentStatusLabel(status);
  }

  visibilityText(visibility: number): string {
    return visibilityLabel(visibility);
  }
}

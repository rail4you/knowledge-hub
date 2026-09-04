import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { PracticumService, PracticumProjectDto } from '../../practicum/practicum.service';
import { PracticumChatService, PracticumAgentConfigDto } from '../../practicum/practicum-chat.service';
import { PracticumChatComponent } from '../../practicum/practicum-chat.component';

@Component({
  selector: 'app-practicum-agent-chat',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    NzButtonModule, NzCardModule, NzInputModule, NzSelectModule, NzIconModule, NzTabsModule,
    PracticumChatComponent,
  ],
  templateUrl: './practicum-agent-chat.component.html',
  styleUrls: ['./practicum-agent-chat.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumAgentChatComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly chatService = inject(PracticumChatService);
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly projects = signal<PracticumProjectDto[]>([]);
  selectedProjectId = '';
  selectedProjectTitle = '';
  agentConfigForm: PracticumAgentConfigDto = {};
  agentSaving = false;
  activeTabIndex = 0;

  ngOnInit(): void {
    this.loadProjects();
  }

  private loadProjects(): void {
    this.practicumService.getList({ skipCount: 0, maxResultCount: 200 }).subscribe({
      next: r => {
        this.projects.set(r.items || []);
        if (r.items?.length && !this.selectedProjectId) {
          this.onProjectChange(r.items[0].id);
        }
      },
      error: () => this.message.error('加载实训列表失败'),
    });
  }

  onProjectChange(id: string): void {
    this.selectedProjectId = id;
    const p = this.projects().find(x => x.id === id);
    this.selectedProjectTitle = p?.title || '';
    this.agentConfigForm = {};
    if (id) {
      this.chatService.getAgentConfig(id).subscribe({
        next: c => { this.agentConfigForm = c || {}; this.cdr.markForCheck(); },
        error: () => {},
      });
    }
  }

  get agentDisplayName(): string {
    return this.agentConfigForm.agentName?.trim() || '小智';
  }

  saveAgentConfig(): void {
    if (!this.selectedProjectId) return;
    this.agentSaving = true;
    this.chatService.updateAgentConfig(this.selectedProjectId, {
      agentName: this.agentConfigForm.agentName,
      agentPrompt: this.agentConfigForm.agentPrompt,
    }).subscribe({
      next: () => { this.agentSaving = false; this.message.success('智能体配置已保存'); },
      error: () => { this.agentSaving = false; this.message.error('保存智能体配置失败'); },
    });
  }
}

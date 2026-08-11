import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { forkJoin } from 'rxjs';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import { PracticumSimulationService } from '../../proxy/practicums/simulations/practicum-simulation.service';
import type { PracticumProjectDetailDto, PracticumMaterialDto, PracticumGuidanceRecordDto, PracticumEnrollmentDto } from '../../proxy/practicums/dtos/models';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/dtos/models';
import { SafeResourceUrlPipe } from '../../shared/safe-resource-url.pipe';

@Component({
  selector: 'app-student-practicum-detail',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, FormsModule, RouterModule,
    NzButtonModule, NzIconModule, NzSpinModule, NzTabsModule, NzInputModule, NzModalModule, NzProgressModule, NzEmptyModule,
    SafeResourceUrlPipe,
  ],
  templateUrl: './student-practicum-detail.component.html',
  styleUrls: ['./student-practicum-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentPracticumDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly practicumService = inject(PracticumService);
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly message = inject(NzMessageService);

  readonly detail = signal<PracticumProjectDetailDto | null>(null);
  readonly simulations = signal<PracticumSimulationDto[]>([]);
  readonly loading = signal(true);
  readonly activeTab = signal<'tasks' | 'materials' | 'simulations' | 'guidance'>('tasks');
  readonly submitting = signal(false);
  readonly submitModalVisible = signal(false);
  readonly selectedTaskId = signal<string | null>(null);
  readonly submissionContent = signal('');
  readonly submissionUrl = signal('');

  readonly enrollment = signal<PracticumEnrollmentDto | null>(null);
  readonly guidanceItems = signal<PracticumGuidanceRecordDto[]>([]);
  readonly guidanceLoading = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) return;
    this.loadDetailAndSimulations(id);
  }

  loadDetailAndSimulations(id: string): void {
    this.loading.set(true);
    forkJoin({
      detail: this.practicumService.getDetail(id),
      simulations: this.simulationService.getListByProject(id),
    }).subscribe({
      next: ({ detail, simulations }) => {
        this.detail.set({
          ...detail,
          materials: (detail.materials ?? []).filter(material => material.materialType !== 4),
        });
        this.simulations.set(simulations ?? []);
        this.loading.set(false);
        this.loadMyProgress(id);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载实训详情失败');
      },
    });
  }

  loadMyProgress(projectId: string): void {
    const enrollmentId = this.detail()?.currentUserEnrollmentId;
    this.practicumService.getMyEnrollments().subscribe({
      next: list => {
        const mine = (list || []).find(e => e.projectId === projectId && (enrollmentId ? e.id === enrollmentId : true));
        this.enrollment.set(mine ?? null);
        if (mine?.id) {
          this.loadGuidance(mine.id);
        }
      },
      error: () => {},
    });
  }

  loadGuidance(enrollmentId: string): void {
    this.guidanceLoading.set(true);
    this.practicumService.getGuidanceList(enrollmentId).subscribe({
      next: list => {
        this.guidanceItems.set(list || []);
        this.guidanceLoading.set(false);
      },
      error: () => {
        this.guidanceLoading.set(false);
      },
    });
  }

  hasScore(en: PracticumEnrollmentDto): boolean {
    return en.finalScore != null;
  }

  enroll(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.practicumService.enroll(id).subscribe({
      next: () => {
        this.message.success('报名成功，等待教师审核');
        this.loadDetailAndSimulations(id);
      },
      error: () => this.message.error('报名失败'),
    });
  }

  openSubmit(taskId?: string): void {
    this.selectedTaskId.set(taskId || null);
    this.submissionContent.set('');
    this.submissionUrl.set('');
    this.submitModalVisible.set(true);
  }

  submitWork(): void {
    const projectId = this.detail()?.id;
    const taskId = this.selectedTaskId();
    const content = this.submissionContent().trim();
    if (!projectId || !taskId || !content) {
      this.message.warning('请填写提交内容');
      return;
    }
    this.submitting.set(true);
    this.practicumService.createSubmission({
      projectId, taskId,
      content,
      linkUrl: this.submissionUrl().trim() || undefined,
    }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.submitModalVisible.set(false);
        this.message.success('提交成功');
        if (this.detail()?.currentUserEnrollmentId) {
          this.loadMyProgress(this.detail()!.id);
        }
      },
      error: () => { this.submitting.set(false); this.message.error('提交失败'); },
    });
  }

  downloadMaterial(material: PracticumMaterialDto): void {
    if (!material.resourceUrl) return;
    window.open(material.resourceUrl, '_blank');
  }

  openChat(): void {
    const id = this.detail()?.id;
    if (!id) return;
    this.router.navigate(['/student/practicums', id, 'chat']);
  }
}

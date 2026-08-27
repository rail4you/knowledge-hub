import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RestService } from '@abp/ng.core';
import { firstValueFrom } from 'rxjs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzUploadModule, NzUploadFile } from 'ng-zorro-antd/upload';
import { ChunkUploadService } from '../../proxy/controllers/chunk-upload.service';
import type { CompleteUploadResultDto } from '../../proxy/resources/models';
import { OssUploadService } from '../../shared/oss-upload.service';
import { PracticumService, PracticumProjectDto } from '../../practicum/practicum.service';
import { PracticumSimulationService } from '../../proxy/practicums/simulations/practicum-simulation.service';
import type { PracticumSimulationDto } from '../../proxy/practicums/simulations/dtos/models';
import { PracticumSimulationStatus } from '../../proxy/practicums/simulations/enums/practicum-simulation-status.enum';

@Component({
  selector: 'app-practicum-simulations',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    NzButtonModule, NzCardModule, NzEmptyModule, NzInputModule, NzModalModule, NzSelectModule,
    NzTableModule, NzTagModule, NzIconModule, NzUploadModule,
  ],
  templateUrl: './practicum-simulations.component.html',
  styleUrls: ['./practicum-simulations.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PracticumSimulationsComponent implements OnInit {
  private readonly practicumService = inject(PracticumService);
  private readonly simulationService = inject(PracticumSimulationService);
  private readonly chunkUploadService = inject(ChunkUploadService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly restService = inject(RestService);
  private readonly message = inject(NzMessageService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly simulationStatuses = PracticumSimulationStatus;

  /** 打包说明的展开状态：默认折叠，只显示标题与概述 */
  readonly noteExpanded = signal(false);

  readonly projects = signal<PracticumProjectDto[]>([]);
  selectedProjectId = '';
  selectedProjectTitle = '';
  readonly simulations = signal<PracticumSimulationDto[]>([]);

  readonly simulationModalVisible = signal(false);
  readonly simulationModalDraft = signal<{
    editingId: string | null;
    name: string;
    description: string;
    coverUrl: string;
    coverFileList: NzUploadFile[];
    coverUploading: boolean;
    file: File | null;
    fileName: string;
    uploading: boolean;
    saving: boolean;
    uploadProgress: number;
  } | null>(null);

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
    this.simulations.set([]);
    if (id) this.loadSimulations(id);
  }

  private loadSimulations(projectId: string): void {
    this.simulationService.getListByProject(projectId).subscribe({
      next: list => { this.simulations.set(list || []); this.cdr.markForCheck(); },
      error: () => this.message.error('加载仿真镜像失败'),
    });
  }

  openAddSimulationModal(): void {
    this.simulationModalDraft.set({
      editingId: null,
      name: '',
      description: '',
      coverUrl: '',
      coverFileList: [],
      coverUploading: false,
      file: null,
      fileName: '',
      uploading: false,
      saving: false,
      uploadProgress: 0,
    });
    this.simulationModalVisible.set(true);
  }

  openEditSimulationModal(item: PracticumSimulationDto): void {
    const coverFileList = item.coverUrl ? [{
      uid: 'sim-cover-existing',
      name: item.name || 'cover',
      status: 'done' as const,
      url: item.coverUrl,
    }] : [];
    this.simulationModalDraft.set({
      editingId: item.id ?? null,
      name: item.name ?? '',
      description: item.description ?? '',
      coverUrl: item.coverUrl ?? '',
      coverFileList,
      coverUploading: false,
      file: null,
      fileName: '',
      uploading: false,
      saving: false,
      uploadProgress: 0,
    });
    this.simulationModalVisible.set(true);
  }

  closeSimulationModal(): void {
    this.simulationModalVisible.set(false);
  }

  onSimulationFileChange(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    if (!file) return;
    if (!file.name.toLowerCase().endsWith('.zip')) {
      this.message.error('仿真构建必须是 ZIP 文件');
      input.value = '';
      return;
    }
    if (file.size > 500 * 1024 * 1024) {
      this.message.error('ZIP 文件不能超过 500MB');
      input.value = '';
      return;
    }
    const draft = this.simulationModalDraft();
    if (draft) this.simulationModalDraft.set({ ...draft, file, fileName: file.name });
  }

  beforeSimulationCoverUpload = (file: NzUploadFile): boolean => {
    const rawFile = file as any as File;
    const allowed = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'image/bmp'];
    if (!allowed.includes(rawFile.type)) {
      this.message.error('封面仅支持 JPG/PNG/GIF/WebP/BMP 格式');
      return false;
    }
    if (rawFile.size > 10 * 1024 * 1024) {
      this.message.error('封面大小不能超过 10MB');
      return false;
    }
    const draft = this.simulationModalDraft();
    if (!draft) return false;
    this.simulationModalDraft.set({ ...draft, coverUploading: true });
    this.ossUploadService.uploadImage(rawFile).subscribe({
      next: (res) => {
        const d = this.simulationModalDraft();
        if (d) {
          this.simulationModalDraft.set({
            ...d,
            coverUploading: false,
            coverUrl: res.url,
            coverFileList: [{ uid: res.objectKey, name: res.originalFileName, status: 'done', url: res.url }],
          });
        }
        this.message.success('封面上传成功');
      },
      error: () => {
        const d = this.simulationModalDraft();
        if (d) this.simulationModalDraft.set({ ...d, coverUploading: false, coverFileList: [] });
        this.message.error('封面上传失败');
      },
    });
    return false;
  };

  removeSimulationCover = (): boolean => {
    const draft = this.simulationModalDraft();
    if (draft) this.simulationModalDraft.set({ ...draft, coverUrl: '', coverFileList: [] });
    return true;
  };

  async saveSimulationModal(): Promise<void> {
    const draft = this.simulationModalDraft();
    if (!draft) return;
    const projectId = this.selectedProjectId;
    if (!projectId || !draft.name.trim()) {
      this.message.warning('请填写仿真名称');
      return;
    }
    if (!draft.editingId && !draft.file) {
      this.message.warning('请先选择仿真 ZIP 文件');
      return;
    }

    this.simulationModalDraft.set({ ...draft, saving: true });
    try {
      if (draft.editingId) {
        await firstValueFrom(this.simulationService.update(draft.editingId, {
          name: draft.name.trim(),
          description: draft.description.trim() || undefined,
          coverUrl: draft.coverUrl.trim() || undefined,
          sortOrder: 0,
        }));
        this.message.success('仿真镜像信息已更新');
      } else {
        const upload = await this.uploadSimulationFile(draft.file!);
        if (!upload.filePath) throw new Error('上传完成但未返回文件路径');
        await firstValueFrom(this.simulationService.create({
          projectId,
          name: draft.name.trim(),
          description: draft.description.trim() || undefined,
          coverUrl: draft.coverUrl.trim() || undefined,
          uploadedFilePath: upload.filePath,
        }));
        this.message.success('仿真镜像已上传并导入');
      }
      this.simulationModalVisible.set(false);
      this.loadSimulations(projectId);
    } catch (error: any) {
      console.error('[practicum-simulations] save failed', error);
      this.message.error(error?.error?.error?.message || error?.message || '仿真镜像保存失败');
    } finally {
      const d = this.simulationModalDraft();
      if (d) this.simulationModalDraft.set({ ...d, saving: false });
    }
  }

  private async uploadSimulationFile(file: File): Promise<CompleteUploadResultDto> {
    const chunkSize = 1024 * 1024;
    const draft = this.simulationModalDraft();
    if (draft) this.simulationModalDraft.set({ ...draft, uploading: true, uploadProgress: 0 });
    try {
      const initiated = await firstValueFrom(this.chunkUploadService.initiateUploadByInput({
        fileName: file.name,
        totalSize: file.size,
        chunkSize,
      }));
      if (!initiated?.uploadId || !initiated.totalChunks) throw new Error('无法初始化分片上传');

      for (let chunkNumber = 0; chunkNumber < initiated.totalChunks; chunkNumber++) {
        const start = chunkNumber * chunkSize;
        const chunk = file.slice(start, Math.min(start + chunkSize, file.size));
        const formData = new FormData();
        formData.append('file', chunk, file.name);
        formData.append('uploadId', initiated.uploadId);
        formData.append('fileName', file.name);
        formData.append('chunkNumber', String(chunkNumber));
        const uploaded = await firstValueFrom(this.restService.request<any, boolean>({
          method: 'POST',
          url: '/api/app/chunk-upload/upload',
          body: formData,
        }));
        if (!uploaded) throw new Error(`第 ${chunkNumber + 1} 个分片上传失败`);
        const d = this.simulationModalDraft();
        if (d) this.simulationModalDraft.set({ ...d, uploadProgress: Math.round((chunkNumber + 1) / initiated.totalChunks * 100) });
      }

      return await firstValueFrom(this.chunkUploadService.completeUploadByInput({
        uploadId: initiated.uploadId,
        fileName: file.name,
        totalChunks: initiated.totalChunks,
      }));
    } finally {
      const d = this.simulationModalDraft();
      if (d) this.simulationModalDraft.set({ ...d, uploading: false });
    }
  }

  deleteSimulation(item: PracticumSimulationDto): void {
    if (!item.id || !window.confirm(`确定删除仿真镜像"${item.name || item.slug}"吗?`)) return;
    this.simulationService.delete(item.id).subscribe({
      next: () => {
        this.message.success('仿真镜像已删除');
        if (this.selectedProjectId) this.loadSimulations(this.selectedProjectId);
      },
      error: () => this.message.error('仿真镜像删除失败'),
    });
  }

  simulationStatusLabel(status?: PracticumSimulationStatus): string {
    if (status === PracticumSimulationStatus.Ready) return '已就绪';
    if (status === PracticumSimulationStatus.Processing) return '处理中';
    if (status === PracticumSimulationStatus.Invalid) return '无效';
    return '未知';
  }

  simulationStatusColor(status?: PracticumSimulationStatus): string {
    if (status === PracticumSimulationStatus.Ready) return 'success';
    if (status === PracticumSimulationStatus.Invalid) return 'error';
    if (status === PracticumSimulationStatus.Processing) return 'processing';
    return 'warning';
  }
}

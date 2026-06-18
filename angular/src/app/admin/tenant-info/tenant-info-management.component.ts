import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzUploadModule } from 'ng-zorro-antd/upload';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { TenantInfoService } from '../../proxy/tenant-infos/tenant-info.service';
import { CourseService } from '../../proxy/courses/course.service';
import { MajorService } from '../../proxy/majors/major.service';
import { OssUploadService, OssUploadResultDto } from '../../shared/oss-upload.service';

import type {
  TenantInfoDto,
  CreateUpdateTenantInfoDto,
  SpecialProjectItem,
  TenantKnowledgeGraphDto,
} from '../../proxy/tenant-infos/dtos/models';
import type { MajorDto } from '../../proxy/majors/dtos/models';

@Component({
  selector: 'app-tenant-info-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzCardModule,
    NzFormModule,
    NzInputModule,
    NzInputNumberModule,
    NzModalModule,
    NzSelectModule,
    NzTagModule,
    NzIconModule,
    NzEmptyModule,
    NzDividerModule,
    NzUploadModule,
    NzProgressModule,
  ],
  templateUrl: './tenant-info-management.component.html',
  styleUrls: ['./tenant-info-management.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TenantInfoManagementComponent implements OnInit {
  private readonly tenantInfoService = inject(TenantInfoService);
  private readonly courseService = inject(CourseService);
  private readonly majorService = inject(MajorService);
  private readonly ossUploadService = inject(OssUploadService);
  private readonly message = inject(NzMessageService);

  readonly tenantInfo = signal<TenantInfoDto | null>(null);
  readonly knowledgeGraph = signal<TenantKnowledgeGraphDto | null>(null);
  readonly majors = signal<MajorDto[]>([]);
  readonly coursesByMajor = signal<Map<string, any[]>>(new Map());
  readonly saving = signal(false);

  // Upload state
  readonly uploading = signal(false);
  readonly coverImages = signal<string[]>([]);

  form: CreateUpdateTenantInfoDto = this.createEmptyForm();

  ngOnInit(): void {
    this.loadData();
  }

  private createEmptyForm(): CreateUpdateTenantInfoDto {
    return {
      name: '',
      type: 0,
      description: null,
      coverImageList: [],
      talentTrainingPlan: null,
      professionalTeachingStandards: null,
      specialProjectList: [],
    };
  }

  private loadData(): void {
    this.tenantInfoService.getCurrent().subscribe({
      next: (res) => {
        this.tenantInfo.set(res);
        this.form = {
          name: res.name || '',
          type: res.type ?? 0,
          description: res.description || null,
          coverImageList: res.coverImageList || [],
          talentTrainingPlan: res.talentTrainingPlan || null,
          professionalTeachingStandards: res.professionalTeachingStandards || null,
          specialProjectList: (res.specialProjectList || []).map((x: any) => ({
            title: x.title || '',
            description: x.description || null,
          })),
        };
        this.coverImages.set(res.coverImageList || []);
      },
      error: () => this.message.error('加载租户信息失败'),
    });

    this.tenantInfoService.getCurrentKnowledgeGraph().subscribe({
      next: (res) => this.knowledgeGraph.set(res),
      error: () => {},
    });

    this.majorService.getList({ maxResultCount: 200, skipCount: 0 }).subscribe({
      next: (res) => {
        this.majors.set(res.items || []);
        this.loadCoursesForMajors(res.items || []);
      },
      error: () => {},
    });
  }

  private loadCoursesForMajors(majors: any[]): void {
    const map = new Map<string, any[]>();
    let loaded = 0;
    if (majors.length === 0) {
      this.coursesByMajor.set(map);
      return;
    }
    for (const major of majors) {
      this.courseService
        .getList({ majorId: major.id, maxResultCount: 200, skipCount: 0 })
        .subscribe({
          next: (res) => {
            map.set(major.id, res.items || []);
            loaded++;
            if (loaded >= majors.length) this.coursesByMajor.set(map);
          },
          error: () => {
            map.set(major.id, []);
            loaded++;
            if (loaded >= majors.length) this.coursesByMajor.set(map);
          },
        });
    }
  }

  getCoursesByMajor(majorId: string): any[] | null {
    return this.coursesByMajor().get(majorId) || null;
  }

  // ── OSS Upload ──

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    if (!file.type.startsWith('image/')) {
      this.message.error('只能上传图片文件');
      return;
    }
    if (file.size / 1024 / 1024 > 5) {
      this.message.error('图片大小不能超过 5MB');
      return;
    }
    this.doUpload(file);
    input.value = ''; // Reset so same file can be re-selected
  }

  private doUpload(file: File): void {
    this.uploading.set(true);
    this.ossUploadService.uploadImage(file).subscribe({
      next: (res: OssUploadResultDto) => {
        const url = res.url;
        const newImages = [...this.coverImages(), url];
        this.coverImages.set(newImages);
        this.form.coverImageList = newImages;
        this.uploading.set(false);
        this.message.success('图片上传成功');
      },
      error: () => {
        this.uploading.set(false);
        this.message.error('图片上传失败，请重试');
      },
    });
  }

  removeImage(index: number): void {
    const newImages = this.coverImages().filter((_, i) => i !== index);
    this.coverImages.set(newImages);
    this.form.coverImageList = newImages;
  }

  // ── Save ──

  save(): void {
    if (!this.form.name?.trim()) {
      this.message.warning('名称不能为空');
      return;
    }

    this.form.coverImageList = this.coverImages();

    this.saving.set(true);
    const tenantId = this.tenantInfo()?.tenantId;
    const save$ = tenantId
      ? this.tenantInfoService.saveByTenantId(tenantId, this.form)
      : this.tenantInfoService.saveCurrent(this.form);
    save$.subscribe({
      next: () => {
        this.saving.set(false);
        this.message.success('租户信息已保存');
        this.loadData();
      },
      error: (err: any) => {
        this.saving.set(false);
        this.message.error(err?.error?.error?.message || err?.message || '保存失败');
      },
    });
  }

  addSpecialProject(): void {
    this.form.specialProjectList = [
      ...this.form.specialProjectList,
      { title: '', description: null },
    ];
  }

  removeSpecialProject(index: number): void {
    this.form.specialProjectList = this.form.specialProjectList.filter(
      (_: any, i: number) => i !== index
    );
  }
}

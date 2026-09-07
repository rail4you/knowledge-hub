import { Component, signal, inject, OnInit, ChangeDetectionStrategy, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { LocalizationPipe, Rest, RestService } from '@abp/ng.core';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzSwitchModule } from 'ng-zorro-antd/switch';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { CourseResourceService } from '../../proxy/courses/course-resource.service';
import { ResourceService } from '../../proxy/resources/resource.service';
import { ResourceType } from '../../proxy/resources/enums/resource-type.enum';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { CourseResourceDto } from '../../proxy/courses/dtos/models';
import type { ResourceDto } from '../../proxy/resources/models';

@Component({
  selector: 'app-course-resource',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzTagModule,
    NzIconModule,
    NzSpinModule,
    NzEmptyModule,
    NzSelectModule,
    NzTableModule,
    NzSwitchModule,
  ],
  templateUrl: './course-resource.component.html',
  styleUrls: ['./course-resource.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseResourceComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly courseResourceService = inject(CourseResourceService);
  private readonly resourceService = inject(ResourceService);
  private readonly restService = inject(RestService);
  private readonly message = inject(NzMessageService);

  courses = signal<CourseDto[]>([]);
  selectedCourseId = signal<string | null>(null);

  // 当前课程已关联的资源
  courseResources = signal<CourseResourceDto[]>([]);
  // 资源库中「联盟审核通过」的资源
  libraryResources = signal<ResourceDto[]>([]);
  loading = signal(false);
  libraryLoading = signal(false);
  searchText = signal('');

  // 勾选待添加的资源
  selectedResourceIds = signal<Set<string>>(new Set());

  // 过滤掉已在本课程关联过的资源
  availableResources = computed(() => {
    const linkedIds = new Set(
      this.courseResources()
        .map(r => r.resourceId)
        .filter((id): id is string => !!id)
    );
    const search = this.searchText().toLowerCase();
    let available = this.libraryResources().filter(r => !linkedIds.has(r.id));
    if (search) {
      available = available.filter(r =>
        r.name?.toLowerCase().includes(search) ||
        r.description?.toLowerCase().includes(search)
      );
    }
    return available;
  });

  selectedCount = computed(() => this.selectedResourceIds().size);

  isAllChecked = computed(() => {
    const avail = this.availableResources();
    return avail.length > 0 && avail.every(r => this.selectedResourceIds().has(r.id));
  });

  isIndeterminate = computed(() => {
    const avail = this.availableResources();
    if (avail.length === 0) return false;
    const checked = avail.filter(r => this.selectedResourceIds().has(r.id)).length;
    return checked > 0 && checked < avail.length;
  });

  ngOnInit() {
    this.loadCourses();
  }

  loadCourses() {
    this.courseService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
      },
    });
  }

  onCourseSelected(courseId: string) {
    this.selectedCourseId.set(courseId);
    this.courseResources.set([]);
    this.searchText.set('');
    this.clearSelection();
    this.loadCourseResources();
    this.loadLibraryResources();
  }

  loadCourseResources() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.courseResourceService.getByCourse(courseId).subscribe({
      next: (data) => {
        this.courseResources.set(data || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载课程资源失败');
      },
    });
  }

  loadLibraryResources() {
    this.libraryLoading.set(true);
    this.resourceService.getLeagueApproved({ skipCount: 0, maxResultCount: 1000 }).subscribe({
      next: (result) => {
        this.libraryResources.set(result.items || []);
        this.libraryLoading.set(false);
      },
      error: () => {
        this.libraryLoading.set(false);
        this.message.error('加载资源库失败');
      },
    });
  }

  linkResource(resource: ResourceDto) {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.courseResourceService.create({
      courseId: courseId,
      resourceId: resource.id,
      displayName: resource.name ?? '',
      sortOrder: 0,
      isRecommended: false,
    } as any).subscribe({
      next: () => {
        this.message.success('资源已关联到课程');
        this.clearSelection();
        this.loadCourseResources();
      },
      error: (err) => {
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          err?.message ||
          '';
        this.message.error('关联失败：' + (detail || '未知错误'));
      },
    });
  }

  isSelected(resourceId: string): boolean {
    return this.selectedResourceIds().has(resourceId);
  }

  onItemChecked(resourceId: string, checked: boolean) {
    const set = new Set(this.selectedResourceIds());
    if (checked) {
      set.add(resourceId);
    } else {
      set.delete(resourceId);
    }
    this.selectedResourceIds.set(set);
  }

  onAllChecked(checked: boolean) {
    const set = new Set(this.selectedResourceIds());
    for (const resource of this.availableResources()) {
      if (checked) {
        set.add(resource.id);
      } else {
        set.delete(resource.id);
      }
    }
    this.selectedResourceIds.set(set);
  }

  clearSelection() {
    this.selectedResourceIds.set(new Set());
  }

  linkSelected() {
    const courseId = this.selectedCourseId();
    if (!courseId) return;
    const ids = [...this.selectedResourceIds()];
    if (ids.length === 0) return;

    const resourceById = new Map(this.libraryResources().map(r => [r.id, r]));
    const tasks = ids.map(id => {
      const resource = resourceById.get(id);
      return this.courseResourceService.create({
        courseId: courseId,
        resourceId: id,
        displayName: resource?.name ?? '',
        sortOrder: 0,
        isRecommended: false,
      } as any);
    });

    forkJoin(tasks).subscribe({
      next: () => {
        this.message.success(`已关联 ${ids.length} 个资源到课程`);
        this.clearSelection();
        this.loadCourseResources();
      },
      error: (err) => {
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          err?.message ||
          '';
        this.message.error('关联失败：' + (detail || '未知错误'));
        this.loadCourseResources();
      },
    });
  }

  /** 课程资源推荐开关：不改代理文件，直接调 PUT（UpdateAsync 由 ABP 常规控制器暴露） */
  toggleRecommend(courseResource: CourseResourceDto, checked: boolean) {
    if (!courseResource.id) return;
    const body = {
      displayName: courseResource.displayName ?? '',
      sortOrder: courseResource.sortOrder ?? 0,
      isRecommended: checked,
    };
    this.restService
      .request<any, CourseResourceDto>(
        {
          method: 'PUT',
          url: `/api/app/course-resource/${courseResource.id}`,
          body,
        },
        { apiName: 'KnowledgeHub' } as Partial<Rest.Config>
      )
      .subscribe({
        next: () => {
          this.courseResources.update(list =>
            list.map(r => (r.id === courseResource.id ? { ...r, isRecommended: checked } : r))
          );
          this.message.success(checked ? '已设为推荐资源' : '已取消推荐');
        },
        error: () => {
          this.message.error('推荐状态更新失败');
          this.loadCourseResources();
        },
      });
  }

  isRecommended(courseResource: CourseResourceDto): boolean {
    return !!(courseResource as any).isRecommended;
  }

  unlinkResource(courseResource: CourseResourceDto) {
    if (!courseResource.id) return;

    this.courseResourceService.delete(courseResource.id).subscribe({
      next: () => {
        this.message.success('已取消关联');
        this.loadCourseResources();
      },
      error: (err) => {
        const detail =
          err?.error?.error?.message ||
          err?.error?.message ||
          err?.message ||
          '';
        this.message.error('取消关联失败：' + (detail || '未知错误'));
      },
    });
  }

  getResourceTypeLabel(type: ResourceType | undefined): string {
    if (type === undefined || type === null) return '';
    const map: Record<number, string> = {
      [ResourceType.Document]: '文档',
      [ResourceType.Video]: '视频',
      [ResourceType.Audio]: '音频',
      [ResourceType.Image]: '图片',
      [ResourceType.PPT]: 'PPT',
    };
    return map[type] ?? '其他';
  }

  getResourceTypeColor(type: ResourceType | undefined): string {
    if (type === undefined || type === null) return 'default';
    const map: Record<number, string> = {
      [ResourceType.Document]: 'blue',
      [ResourceType.Video]: 'purple',
      [ResourceType.Audio]: 'cyan',
      [ResourceType.Image]: 'green',
      [ResourceType.PPT]: 'orange',
    };
    return map[type] ?? 'default';
  }
}

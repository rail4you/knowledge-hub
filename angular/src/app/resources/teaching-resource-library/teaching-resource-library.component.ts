import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpaceModule } from 'ng-zorro-antd/space';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TeachingResourceLibraryService } from '../../proxy/resource-libraries/teaching-resource-library.service';
import { LibraryContentType } from '../../proxy/resource-libraries/library-content-type.enum';
import type {
  AttachLibraryContentDto,
  TeachingLibraryContentDto,
  TeachingLibraryDto,
} from '../../proxy/resource-libraries/dtos/models';
import { ResourceService } from '../../proxy/resources/resource.service';
import type { ResourceDto } from '../../proxy/resources/models';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { PracticumService } from '../../proxy/practicums/practicum.service';
import type { PracticumProjectDto } from '../../proxy/practicums/dtos/models';

/** 可共享的内容候选（本校自有资源 / 课程 / 实训）。 */
interface ContentCandidate {
  id: string;
  name: string;
  extra?: string;
}

/**
 * 教师端「教学资源库建设」。
 *
 * <p>教学资源库 = 文件资源 + 课程 + 实训 的关联集合。教师在这里：
 * - 查看与自己相关的教学资源库（被分配的私有库 + 本校公开库 + 参与共建的库）；
 * - 查看库内内容（含其它学校通过共建库共享进来的内容）；
 * - 把本校的内容共享进库 / 把内容移出库。</p>
 */
@Component({
  selector: 'app-teaching-resource-library',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSpaceModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './teaching-resource-library.component.html',
  styleUrls: ['./teaching-resource-library.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingResourceLibraryComponent implements OnInit {
  private readonly libraryService = inject(TeachingResourceLibraryService);
  private readonly resourceService = inject(ResourceService);
  private readonly courseService = inject(CourseService);
  private readonly practicumService = inject(PracticumService);
  private readonly message = inject(NzMessageService);

  readonly contentTypeOptions = [
    { label: '文件资源', value: LibraryContentType.Resource },
    { label: '课程', value: LibraryContentType.Course },
    { label: '实训', value: LibraryContentType.Practicum },
  ];

  readonly libraries = signal<TeachingLibraryDto[]>([]);
  readonly loading = signal<boolean>(false);

  readonly selectedLibrary = signal<TeachingLibraryDto | null>(null);
  readonly contents = signal<TeachingLibraryContentDto[]>([]);
  readonly contentTotal = signal<number>(0);
  readonly contentLoading = signal<boolean>(false);
  readonly contentPageIndex = signal<number>(1);
  readonly contentPageSize = signal<number>(10);
  readonly contentTypeFilter = signal<LibraryContentType | null>(null);
  readonly contentKeyword = signal<string>('');

  /** 共享内容弹窗 */
  readonly shareModalVisible = signal<boolean>(false);
  readonly shareContentType = signal<LibraryContentType>(LibraryContentType.Resource);
  readonly shareCandidates = signal<ContentCandidate[]>([]);
  readonly shareCandidatesLoading = signal<boolean>(false);
  readonly shareKeyword = signal<string>('');
  readonly selectedCandidateId = signal<string | null>(null);
  readonly sharing = signal<boolean>(false);

  ngOnInit(): void {
    this.loadLibraries();
  }

  loadLibraries(): void {
    this.loading.set(true);
    this.libraryService.getMyLibraries({}).subscribe({
      next: (list) => {
        this.libraries.set(list || []);
        this.loading.set(false);
        const current = this.selectedLibrary();
        const stillVisible = current
          ? (list || []).find((x) => x.id === current.id)
          : undefined;
        if (stillVisible) {
          this.selectedLibrary.set(stillVisible);
        } else if ((list || []).length > 0) {
          this.selectLibrary((list || [])[0]);
        }
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载教学资源库失败');
      },
    });
  }

  selectLibrary(item: TeachingLibraryDto): void {
    this.selectedLibrary.set(item);
    this.contentPageIndex.set(1);
    this.loadContents();
  }

  loadContents(): void {
    const library = this.selectedLibrary();
    if (!library?.id) {
      return;
    }

    this.contentLoading.set(true);
    this.libraryService
      .getContents(library.id, {
        maxResultCount: this.contentPageSize(),
        skipCount: (this.contentPageIndex() - 1) * this.contentPageSize(),
        contentType: this.contentTypeFilter() ?? undefined,
        filter: this.contentKeyword()?.trim() || undefined,
      })
      .subscribe({
        next: (result) => {
          this.contents.set(result?.items || []);
          this.contentTotal.set(result?.totalCount || 0);
          this.contentLoading.set(false);
        },
        error: () => {
          this.contents.set([]);
          this.contentTotal.set(0);
          this.contentLoading.set(false);
        },
      });
  }

  onContentPageChange(page: number): void {
    this.contentPageIndex.set(page);
    this.loadContents();
  }

  onContentTypeChange(type: LibraryContentType | null): void {
    this.contentTypeFilter.set(type);
    this.contentPageIndex.set(1);
    this.loadContents();
  }

  onContentSearch(): void {
    this.contentPageIndex.set(1);
    this.loadContents();
  }

  /** 打开「共享内容到库」弹窗。 */
  openShareModal(): void {
    const library = this.selectedLibrary();
    if (!library?.id) {
      return;
    }
    if (!library.canContribute) {
      this.message.warning('你没有向该教学资源库共享内容的权限');
      return;
    }

    this.shareModalVisible.set(true);
    this.selectedCandidateId.set(null);
    this.shareKeyword.set('');
    this.loadCandidates();
  }

  closeShareModal(): void {
    this.shareModalVisible.set(false);
  }

  onShareContentTypeChange(type: LibraryContentType): void {
    this.shareContentType.set(type);
    this.shareKeyword.set('');
    this.selectedCandidateId.set(null);
    this.loadCandidates();
  }

  /** 加载可共享的内容候选（本校自有资源 / 课程 / 实训）。 */
  loadCandidates(): void {
    this.shareCandidatesLoading.set(true);
    const keyword = this.shareKeyword()?.trim() || undefined;

    if (this.shareContentType() === LibraryContentType.Resource) {
      this.resourceService
        .getFilteredList({ maxResultCount: 100, skipCount: 0, filter: keyword })
        .subscribe({
          next: (page) => {
            this.shareCandidates.set(
              (page?.items || []).map((r: ResourceDto) => ({
                id: r.id!,
                name: r.name || '未命名资源',
                extra: r.fileExtension || '',
              })),
            );
            this.shareCandidatesLoading.set(false);
          },
          error: () => {
            this.shareCandidates.set([]);
            this.shareCandidatesLoading.set(false);
          },
        });
      return;
    }

    if (this.shareContentType() === LibraryContentType.Course) {
      this.courseService
        .getList({ maxResultCount: 100, skipCount: 0, filter: keyword } as any)
        .subscribe({
          next: (page: any) => {
            this.shareCandidates.set(
              (page?.items || []).map((c: CourseDto) => ({
                id: c.id!,
                name: c.title || '未命名课程',
                extra: c.semester || '',
              })),
            );
            this.shareCandidatesLoading.set(false);
          },
          error: () => {
            this.shareCandidates.set([]);
            this.shareCandidatesLoading.set(false);
          },
        });
      return;
    }

    this.practicumService
      .getList({ maxResultCount: 100, skipCount: 0, filter: keyword } as any)
      .subscribe({
        next: (page: any) => {
          this.shareCandidates.set(
            (page?.items || []).map((p: PracticumProjectDto) => ({
              id: p.id!,
              name: p.title || '未命名实训',
              extra: p.major || '',
            })),
          );
          this.shareCandidatesLoading.set(false);
        },
        error: () => {
          this.shareCandidates.set([]);
          this.shareCandidatesLoading.set(false);
        },
      });
  }

  /** 确认把选中的内容共享进库。 */
  confirmShare(): void {
    const library = this.selectedLibrary();
    const contentId = this.selectedCandidateId();
    if (!library?.id || !contentId) {
      this.message.warning('请选择要共享的内容');
      return;
    }

    const input: AttachLibraryContentDto = {
      contentType: this.shareContentType(),
      contentId,
    };

    this.sharing.set(true);
    this.libraryService.attach(library.id, input).subscribe({
      next: () => {
        this.sharing.set(false);
        this.shareModalVisible.set(false);
        this.message.success('已共享到教学资源库');
        this.loadContents();
        this.loadLibraries();
      },
      error: (err) => {
        this.sharing.set(false);
        this.message.error(
          err?.error?.error?.message || '共享到教学资源库失败',
        );
      },
    });
  }

  /** 把内容移出教学资源库。 */
  detach(item: TeachingLibraryContentDto): void {
    const library = this.selectedLibrary();
    if (!library?.id || !item.id) {
      return;
    }

    this.libraryService
      .detach(library.id, item.contentType!, item.id)
      .subscribe({
        next: () => {
          this.message.success('已移出教学资源库');
          this.loadContents();
          this.loadLibraries();
        },
        error: (err) =>
          this.message.error(
            err?.error?.error?.message || '移出教学资源库失败',
          ),
      });
  }

  visibilityColor(visibility?: number): string {
    switch (visibility) {
      case 1:
        return 'cyan';
      case 2:
        return 'purple';
      default:
        return 'default';
    }
  }
}

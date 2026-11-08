import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { ConfigStateService } from '@abp/ng.core';
import { ManagedCourseService } from '../../learning/course-list/managed-course.service';
import { getCurrentUserRoles } from '../../auth/current-user.utils';
import {
  ResourceConversionService,
  type CourseConvertibleResource,
  type LibraryConvertibleResource,
} from './resource-conversion.service';

interface Option {
  id: string;
  name: string;
}

/**
 * 资源互转（课程资源 ⇄ 教学资源库资源）。
 *
 * <p>产品口径：库资源独立于课程，课程里的资源就是课程资源，两类分开处理。
 * 「互转」= <b>复制一份新记录</b>（带来源标记），之后两侧各自维护、互不同步；
 * 需要最新内容时再转一次即可。</p>
 *
 * <p>左栏 = 某门课程的课程资源；右栏 = 某个资源库的库资源。
 * 选中目标课程 / 目标资源库后，即可把任一侧的资源转到另一侧。</p>
 */
@Component({
  selector: 'app-resource-conversion',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzEmptyModule,
    NzIconModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
    NzTooltipModule,
  ],
  templateUrl: './resource-conversion.component.html',
  styleUrls: ['./resource-conversion.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResourceConversionComponent implements OnInit {
  private readonly conversionService = inject(ResourceConversionService);
  private readonly managedCourseService = inject(ManagedCourseService);
  private readonly configState = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);

  readonly courses = signal<Option[]>([]);
  readonly libraries = signal<Option[]>([]);

  readonly selectedCourseId = signal<string | null>(null);
  readonly selectedLibraryId = signal<string | null>(null);

  readonly courseRows = signal<CourseConvertibleResource[]>([]);
  readonly libraryRows = signal<LibraryConvertibleResource[]>([]);
  readonly courseLoading = signal(false);
  readonly libraryLoading = signal(false);

  readonly courseChecked = signal<string[]>([]);
  readonly libraryChecked = signal<string[]>([]);

  readonly converting = signal(false);

  readonly selectedCourseName = computed(
    () => this.courses().find(c => c.id === this.selectedCourseId())?.name || '',
  );
  readonly selectedLibraryName = computed(
    () => this.libraries().find(l => l.id === this.selectedLibraryId())?.name || '',
  );

  ngOnInit(): void {
    // 课程：教师只列自己创建的课程（管理员看本校全部），与「课程管理」同一口径
    const roles = getCurrentUserRoles(this.configState);
    const isAdmin = ['admin', 'SchoolAdmin', 'LeagueAdmin'].some(r => roles.includes(r));
    this.managedCourseService
      .getList({ onlyMine: isAdmin ? undefined : true, maxResultCount: 300, skipCount: 0 })
      .subscribe({
        next: res =>
          this.courses.set((res.items || []).map(c => ({ id: c.id as string, name: c.title || '未命名课程' }))),
        error: () => this.courses.set([]),
      });

    // 资源库：本人参与/含自己课程的库 ∪ 自己资源已共享进去的库
    // （后者即使没被分配进库，也要能打开核对里面自己的内容）
    this.conversionService.getMyLibraries().subscribe({
      next: list =>
        this.libraries.set(
          (list || []).map(l => ({
            id: l.id as string,
            name: l.isMine ? (l.name || '未命名资源库') : `${l.name || '未命名资源库'}（含我的资源）`,
          })),
        ),
      error: () => this.libraries.set([]),
    });
  }

  // ── 选择器 ──
  onCourseChange(courseId: string | null): void {
    this.selectedCourseId.set(courseId || null);
    this.courseChecked.set([]);
    this.loadCourseResources();
  }

  onLibraryChange(libraryId: string | null): void {
    this.selectedLibraryId.set(libraryId || null);
    this.libraryChecked.set([]);
    // 切换目标库后，已勾选但该库已共享过的行要取消勾选
    this.courseChecked.set(
      this.courseChecked().filter(id => {
        const row = this.courseRows().find(r => r.resourceId === id);
        return row ? !this.isInSelectedLibrary(row) : true;
      }),
    );
    this.loadLibraryResources();
  }

  loadCourseResources(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.courseRows.set([]);
      return;
    }
    this.courseLoading.set(true);
    this.conversionService.getCourseResources(courseId, undefined, 0, 200).subscribe({
      next: res => {
        this.courseRows.set(res.items || []);
        this.courseLoading.set(false);
      },
      error: () => {
        this.courseRows.set([]);
        this.courseLoading.set(false);
      },
    });
  }

  loadLibraryResources(): void {
    const libraryId = this.selectedLibraryId();
    if (!libraryId) {
      this.libraryRows.set([]);
      return;
    }
    this.libraryLoading.set(true);
    this.conversionService.getLibraryResources(libraryId, undefined, 0, 200).subscribe({
      next: res => {
        this.libraryRows.set(res.items || []);
        this.libraryLoading.set(false);
      },
      error: () => {
        this.libraryRows.set([]);
        this.libraryLoading.set(false);
      },
    });
  }

  // ── 勾选 ──
  toggleCourse(id: string, checked: boolean): void {
    const row = this.courseRows().find(r => r.resourceId === id);
    if (checked && row && !this.canSelectCourseRow(row)) {
      return;
    }
    const cur = this.courseChecked();
    this.courseChecked.set(checked ? [...new Set([...cur, id])] : cur.filter(x => x !== id));
  }

  toggleLibrary(id: string, checked: boolean): void {
    const row = this.libraryRows().find(r => r.resourceId === id);
    if (checked && row && !this.isApproved(row.status)) {
      return;
    }
    const cur = this.libraryChecked();
    this.libraryChecked.set(checked ? [...new Set([...cur, id])] : cur.filter(x => x !== id));
  }

  /** 是否审核通过（院校通过 2 / 联盟通过 3）——只有通过的库资源可以转成课程资源。 */
  isApproved(status?: number): boolean {
    return status === 2 || status === 3;
  }

  /** 库资源状态文案（待审核的资源也列出来，方便老师跟踪自己的共享进度）。 */
  libraryStatusLabel(status?: number): string {
    switch (status) {
      case 0:
        return '草稿';
      case 1:
        return '待审核';
      case 2:
      case 3:
        return '已通过';
      case 4:
        return '已驳回';
      case 5:
        return '已隐藏';
      default:
        return '—';
    }
  }

  libraryStatusColor(status?: number): string {
    switch (status) {
      case 1:
        return 'gold';
      case 2:
      case 3:
        return 'green';
      case 4:
        return 'red';
      default:
        return 'default';
    }
  }

  /** 待审核 / 已驳回的库资源不可勾选转入课程。 */
  canSelectLibraryRow(row: LibraryConvertibleResource): boolean {
    return this.isApproved(row.status);
  }

  /**
   * 某行是否已在「当前选中的目标资源库」里共享过。
   * 未选目标库时一律可选（不同库之间允许各自共享一份）。
   */
  isInSelectedLibrary(row: CourseConvertibleResource): boolean {
    const libId = this.selectedLibraryId();
    if (!libId) {
      return false;
    }
    return (row.sharedLibraryIds || []).includes(libId);
  }

  /** 可勾选的课程资源行（排除已在目标库共享过的）。 */
  private selectableCourseRows(): CourseConvertibleResource[] {
    return this.courseRows().filter(r => !this.isInSelectedLibrary(r));
  }

  allCoursesChecked(): boolean {
    const rows = this.selectableCourseRows();
    return rows.length > 0 && rows.every(r => this.courseChecked().includes(r.resourceId));
  }

  toggleAllCourses(checked: boolean): void {
    this.courseChecked.set(checked ? this.selectableCourseRows().map(r => r.resourceId) : []);
  }

  /**
   * 课程资源行是否可勾选（可共享去资源库）：
   * ① 必须是自己新建的（从资源库转入的副本是库内容回流，不允许再共享回库）；
   * ② 必须审核通过（未通过的先等审核）；
   * ③ 不能已在「当前选中的目标资源库」里共享过。
   */
  canSelectCourseRow(row: CourseConvertibleResource): boolean {
    return row.sourceType === 0 && this.isApproved(row.status) && !this.isInSelectedLibrary(row);
  }

  /** 从资源库转入的课程资源（仅展示、跟踪审核进度）。 */
  isFromLibrary(row: CourseConvertibleResource): boolean {
    return row.sourceType === 2;
  }

  /** 课程资源状态标签。 */
  resourceStatusLabel(status?: number): string {
    switch (status) {
      case 0:
        return '草稿';
      case 1:
        return '待审核';
      case 2:
      case 3:
        return '已通过';
      case 4:
        return '已驳回';
      case 5:
        return '已隐藏';
      default:
        return '—';
    }
  }

  resourceStatusColor(status?: number): string {
    switch (status) {
      case 1:
        return 'gold';
      case 2:
      case 3:
        return 'green';
      case 4:
        return 'red';
      default:
        return 'default';
    }
  }

  /** 可转入课程的库资源行（仅审核通过的）。 */
  private selectableLibraryRows(): LibraryConvertibleResource[] {
    return this.libraryRows().filter(r => this.isApproved(r.status));
  }

  allLibrariesChecked(): boolean {
    const rows = this.selectableLibraryRows();
    return rows.length > 0 && rows.every(r => this.libraryChecked().includes(r.resourceId));
  }

  toggleAllLibraries(checked: boolean): void {
    this.libraryChecked.set(checked ? this.selectableLibraryRows().map(r => r.resourceId) : []);
  }

  // ── 转换 ──
  /** 课程资源 → 教学资源库（需要先选目标资源库）。 */
  convertSelectedToLibrary(): void {
    const libraryId = this.selectedLibraryId();
    if (!libraryId) {
      this.message.warning('请先在右侧选择目标教学资源库');
      return;
    }
    const ids = this.courseChecked();
    if (ids.length === 0) {
      this.message.warning('请先勾选要转换的课程资源');
      return;
    }

    this.converting.set(true);
    this.runSequential(
      ids,
      id => this.conversionService.convertToLibrary({
        resourceId: id,
        sourceCourseId: this.selectedCourseId() || undefined,
        libraryId,
      }),
      () => {
        this.message.success(
          `已把 ${ids.length} 项课程资源转换为教学资源库资源并提交准入审核（目标库：${this.selectedLibraryName()}）`,
        );
        this.courseChecked.set([]);
        this.loadLibraryResources();
        this.loadCourseResources();
      },
    );
  }

  /** 教学资源库资源 → 课程（需要先选目标课程）。 */
  convertSelectedToCourse(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先在左侧选择目标课程');
      return;
    }
    const libraryId = this.selectedLibraryId();
    if (!libraryId) {
      this.message.warning('请先在右侧选择来源教学资源库');
      return;
    }
    const ids = this.libraryChecked();
    if (ids.length === 0) {
      this.message.warning('请先勾选要转换的资源库资源');
      return;
    }

    this.converting.set(true);
    this.runSequential(
      ids,
      id => this.conversionService.convertToCourse({ resourceId: id, libraryId, courseId }),
      () => {
        this.message.success(
          `已把 ${ids.length} 项库资源转换为课程资源（目标课程：${this.selectedCourseName()}，待审核后学生可见）`,
        );
        this.libraryChecked.set([]);
        this.loadCourseResources();
        this.loadLibraryResources();
      },
    );
  }

  /** 逐条串行调用（后端一次一条），全部结束后统一提示。 */
  private runSequential(
    ids: string[],
    call: (id: string) => { subscribe: (o: { next?: () => void; error?: () => void }) => void },
    done: () => void,
  ): void {
    let index = 0;
    let ok = 0;
    let fail = 0;
    const next = () => {
      if (index >= ids.length) {
        this.converting.set(false);
        if (fail > 0) {
          this.message.warning(`${ok} 项成功，${fail} 项失败`);
        }
        done();
        return;
      }
      const id = ids[index++];
      call(id).subscribe({
        next: () => {
          ok++;
          next();
        },
        error: () => {
          fail++;
          next();
        },
      });
    };
    next();
  }

  // ── 展示 ──
  /** 来源列短标签：新建 / 课程 / 资源库 */
  sourceLabel(sourceName?: string | null, sourceType?: number): string {
    switch (sourceType) {
      case 1:
        return '课程';
      case 2:
        return '资源库';
      default:
        return sourceName ? '新建' : '新建';
    }
  }

  /** 来源列悬浮提示：来源课程 / 资源库名称 */
  sourceTitle(sourceName?: string | null): string {
    const name = (sourceName || '新建').trim();
    const idx = name.indexOf('：');
    return idx >= 0 ? name.slice(idx + 1) : name;
  }

  sourceTagColor(sourceType: number): string {
    switch (sourceType) {
      case 1:
        return 'blue';
      case 2:
        return 'purple';
      default:
        return 'default';
    }
  }

  fileSizeText(size?: number | null): string {
    if (!size) return '—';
    const kb = size / 1024;
    if (kb < 1024) return `${kb.toFixed(0)} KB`;
    return `${(kb / 1024).toFixed(1)} MB`;
  }
}

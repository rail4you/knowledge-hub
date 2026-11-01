import { ChangeDetectionStrategy, Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { TeachingResourceLibraryService } from '../../proxy/resource-libraries/teaching-resource-library.service';
import type {
  TeachingLibraryContentDto,
  TeachingLibraryDto,
} from '../../proxy/resource-libraries/dtos/models';
import { LibraryContentType } from '../../proxy/resource-libraries/library-content-type.enum';
import { ResourceLibraryVisibility } from '../../proxy/resource-libraries/resource-library-visibility.enum';
import { ResourceService } from '../../proxy/resources/resource.service';
import { CourseService } from '../../proxy/courses/course.service';
import { MajorService } from '../../proxy/majors/major.service';
import type { MajorLookupDto } from '../../proxy/majors/dtos/models';
import { PracticumService } from '../../practicum/practicum.service';

/** 资源库可见性 → 图标（与资源中心侧边栏保持一致）。 */
const VISIBILITY_ICONS: Record<number, string> = {
  [ResourceLibraryVisibility.Private]: 'lock',
  [ResourceLibraryVisibility.Public]: 'team',
  [ResourceLibraryVisibility.CoBuilt]: 'apartment',
};

/**
 * 可共享进资源库的候选内容（统一视图：资源 / 课程 / 实训）。
 */
interface CandidateRow {
  id: string;
  type: LibraryContentType;
  name: string;
  /** 副信息：文件扩展名 / 专业 */
  extra?: string;
  majorName?: string;
  /** 当前所属资源库 Id（用于「资源库列表过滤」） */
  libraryIds: string[];
  /** 当前所属资源库名称 */
  libraryNames: string[];
}

/**
 * 教学资源库工作台。
 *
 * 面向教师端（挂在系统工作台下方 / 独立页面）：聚合展示「我能看到的全部教学资源库」，
 * 每个库给出资源 / 课程 / 实训的数量汇总，并提供快捷访问与库内操作：
 * - 快捷访问：一键跳到该库的资源、课程、实训列表（带 libraryId 深链）；
 * - 库内操作：统一的「库内容 · 共享」弹窗 —— 一套表格 UI，切换「库内容 / 共享」两个页签，
 *   表格支持 checkbox 多选、搜索框与资源库过滤；共享列表自动排除本库已关联的内容。
 */
@Component({
  selector: 'app-teaching-library-workbench',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzModalModule,
    NzSelectModule,
    NzSpinModule,
    NzTableModule,
    NzTagModule,
  ],
  templateUrl: './teaching-library-workbench.component.html',
  styleUrls: ['./teaching-library-workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TeachingLibraryWorkbenchComponent implements OnInit {
  private readonly libraryService = inject(TeachingResourceLibraryService);
  private readonly resourceService = inject(ResourceService);
  private readonly courseService = inject(CourseService);
  private readonly practicumService = inject(PracticumService);
  private readonly majorService = inject(MajorService);
  private readonly message = inject(NzMessageService);
  private readonly permissionService = inject(PermissionService);
  private readonly configState = inject(ConfigStateService);

  readonly libraries = signal<TeachingLibraryDto[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal(false);
  /** 关键字过滤（库名 / 简介）。 */
  readonly keyword = signal('');

  /** 是否有「资源库建设」写权限（教师角色默认有）。 */
  readonly canBuild = signal(false);

  readonly majors = signal<MajorLookupDto[]>([]);

  /**
   * 嵌入模式：外部页面已有页头时隐藏本区块的标题（只保留汇总行与搜索 / 刷新）。
   */
  readonly embedded = input(false);

  /** 关键字过滤后的库列表。 */
  readonly visibleLibraries = computed<TeachingLibraryDto[]>(() => {
    const kw = this.keyword().trim().toLowerCase();
    const list = this.libraries();
    if (!kw) {
      return list;
    }
    return list.filter(
      l =>
        (l.name || '').toLowerCase().includes(kw) ||
        (l.description || '').toLowerCase().includes(kw) ||
        (l.primaryMajorName || '').toLowerCase().includes(kw) ||
        (l.ownerTenantName || '').toLowerCase().includes(kw),
    );
  });

  /** 资源库数据汇总（当前过滤结果内的库合计）。 */
  readonly summary = computed(() => {
    const list = this.visibleLibraries();
    return {
      libraryCount: list.length,
      privateCount: list.filter(l => l.visibility === ResourceLibraryVisibility.Private).length,
      publicCount: list.filter(l => l.visibility === ResourceLibraryVisibility.Public).length,
      coBuiltCount: list.filter(l => l.visibility === ResourceLibraryVisibility.CoBuilt).length,
      resourceCount: list.reduce((sum, l) => sum + (l.resourceCount || 0), 0),
      courseCount: list.reduce((sum, l) => sum + (l.courseCount || 0), 0),
      practicumCount: list.reduce((sum, l) => sum + (l.practicumCount || 0), 0),
    };
  });

  ngOnInit(): void {
    this.canBuild.set(
      this.permissionService.getGrantedPolicy('KnowledgeHub.ResourceLibraries.Build'),
    );
    this.reload();
    this.majorService.getLookupList().subscribe({
      next: list => this.majors.set(list || []),
      error: () => this.majors.set([]),
    });
  }

  /** 重新加载「我的教学资源库」。 */
  reload(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.libraryService.getMyLibraries({}).subscribe({
      next: list => {
        this.libraries.set(list || []);
        this.loading.set(false);
      },
      error: () => {
        this.libraries.set([]);
        this.loadError.set(true);
        this.loading.set(false);
      },
    });
  }

  // ────────────────────────────── 展示辅助 ──────────────────────────────

  /**
   * 「可见资源库」指标卡：滚动到下方的资源库列表。
   *
   * <para>其余三张卡是普通路由跳转（资源 / 课程 / 实训列表），
   * 这张卡的目标就在本页下方，直接滚过去更自然。</para>
   */
  scrollToLibraryList(): void {
    if (typeof document === 'undefined') return;
    const el = document.getElementById('tlw-library-list');
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  visibilityIcon(lib: TeachingLibraryDto): string {
    return VISIBILITY_ICONS[lib.visibility ?? 0] || 'book';
  }

  visibilityName(lib: TeachingLibraryDto): string {
    return lib.visibilityName || '教学资源库';
  }

  /** 我能否向该库贡献内容（库维度 + 权限维度都要满足）。 */
  canContribute(lib: TeachingLibraryDto): boolean {
    return !!lib.canContribute && this.canBuild();
  }

  // ══════════════════════════════════════════════════════════
  //  统一的「库内容 · 共享」弹窗
  // ══════════════════════════════════════════════════════════

  manageModalVisible = false;
  manageLibrary: TeachingLibraryDto | null = null;
  /** 页签：库内容（已关联）/ 共享资源（可添加）。 */
  manageTab: 'contents' | 'share' = 'contents';

  // ── 库内容（已关联到本库） ──
  readonly contents = signal<TeachingLibraryContentDto[]>([]);
  readonly contentsLoading = signal(false);
  readonly contentChecked = signal<string[]>([]);
  contentTypeFilter: LibraryContentType | null = null;
  contentKeyword = '';

  // ── 共享资源（候选内容） ──
  readonly candidates = signal<CandidateRow[]>([]);
  readonly candidatesLoading = signal(false);
  readonly candidateChecked = signal<string[]>([]);
  readonly shareSubmitting = signal(false);
  shareType: LibraryContentType = LibraryContentType.Resource;
  shareKeyword = '';
  /** 共享列表的「资源库列表过滤」：'' = 全部，'__none__' = 未关联任何库。 */
  shareLibraryFilter = '';

  /** 资源库 facets 是否已加载（候选内容过滤依赖它）。 */
  private facetsLoaded = false;

  /**
   * 「所属资源库」选项：**与文件资源表单里能选用的教学资源库完全同一套数据**
   * （同一接口 `GET /api/app/resource/library-facets`，与资源中心侧边栏同一来源）。
   */
  readonly libraryOptions = signal<{ id: string; name: string }[]>([]);

  /** 允许出现在共享列表里的资源库 Id 集合（= 文件资源表单可关联的那些库）。 */
  private readonly allowedLibraryIds = computed(
    () => new Set(this.libraryOptions().map(l => l.id)),
  );

  /** 本库已关联内容的 key 集合（用于把已关联项排除出共享列表）。 */
  readonly attachedKeys = computed(
    () => new Set(this.contents().map(c => this.rowKey(c.contentType!, c.id!))),
  );

  /** 库内容表格（按类型 + 关键字过滤）。 */
  readonly filteredContents = computed<TeachingLibraryContentDto[]>(() => {
    const kw = this.contentKeyword.trim().toLowerCase();
    const type = this.contentTypeFilter;
    return this.contents().filter(c => {
      if (type !== null && c.contentType !== type) {
        return false;
      }
      if (!kw) {
        return true;
      }
      return [c.name, c.creatorName, c.majorName, c.tenantName].some(
        v => (v || '').toLowerCase().includes(kw),
      );
    });
  });

  /** 共享候选表格（按「所属资源库」过滤；已在本库关联的已被排除）。 */
  readonly filteredCandidates = computed<CandidateRow[]>(() => {
    const filter = this.shareLibraryFilter;
    if (!filter) {
      return this.candidates();
    }
    if (filter === '__none__') {
      return this.candidates().filter(r => r.libraryIds.length === 0);
    }
    return this.candidates().filter(r => r.libraryIds.includes(filter));
  });

  rowKey(type: LibraryContentType, id: string): string {
    return `${type}:${id}`;
  }

  contentKey(item: TeachingLibraryContentDto): string {
    return this.rowKey(item.contentType!, item.id!);
  }

  /** 打开「库内容 · 共享」弹窗。 */
  openManage(lib: TeachingLibraryDto): void {
    this.manageLibrary = lib;
    this.manageTab = 'contents';
    this.contentTypeFilter = null;
    this.contentKeyword = '';
    this.shareType = LibraryContentType.Resource;
    this.shareKeyword = '';
    this.shareLibraryFilter = '';
    this.contentChecked.set([]);
    this.candidateChecked.set([]);
    this.manageModalVisible = true;
    this.loadContents();
    void this.loadFilterLibraries();
  }

  switchTab(tab: 'contents' | 'share'): void {
    this.manageTab = tab;
    if (tab === 'share') {
      void this.loadCandidates();
    }
  }

  /** 加载本库已关联的内容（一次取全量，供两个页签共用）。 */
  loadContents(): void {
    const lib = this.manageLibrary;
    if (!lib?.id) {
      return;
    }
    this.contentsLoading.set(true);
    this.libraryService.getContents(lib.id, { maxResultCount: 500 }).subscribe({
      next: result => {
        this.contents.set(result.items || []);
        this.contentsLoading.set(false);
      },
      error: () => {
        this.contents.set([]);
        this.contentsLoading.set(false);
        this.message.error('库内容加载失败');
      },
    });
  }

  /** 资源库过滤下拉的选项：与文件资源表单同一套数据（资源库侧边栏 facets）。 */
  async loadFilterLibraries(): Promise<void> {
    await new Promise<void>(resolve => {
      this.resourceService.getLibraryFacets().subscribe({
        next: list => {
          this.libraryOptions.set(
            (list || [])
              .filter(f => !!f.libraryId)
              .map(f => ({ id: f.libraryId as string, name: f.name || '未命名资源库' })),
          );
          this.facetsLoaded = true;
          resolve();
        },
        error: () => {
          this.libraryOptions.set([]);
          this.facetsLoaded = true;
          resolve();
        },
      });
    });
  }

  // ── 库内容页签：筛选与勾选 ──

  setContentType(type: LibraryContentType | null): void {
    this.contentTypeFilter = type;
  }

  setContentKeyword(keyword: string): void {
    this.contentKeyword = keyword;
  }

  isContentChecked(item: TeachingLibraryContentDto): boolean {
    return this.contentChecked().includes(this.contentKey(item));
  }

  toggleContent(item: TeachingLibraryContentDto, checked: boolean): void {
    const key = this.contentKey(item);
    const current = this.contentChecked();
    this.contentChecked.set(
      checked ? [...new Set([...current, key])] : current.filter(k => k !== key),
    );
  }

  /** 表头全选：只勾选「可移出库」的行。 */
  allContentsChecked(): boolean {
    const rows = this.filteredContents().filter(c => c.canDetach);
    return rows.length > 0 && rows.every(c => this.isContentChecked(c));
  }

  toggleAllContents(checked: boolean): void {
    const rows = this.filteredContents().filter(c => c.canDetach);
    const keys = rows.map(c => this.contentKey(c));
    const current = this.contentChecked();
    this.contentChecked.set(checked ? [...new Set([...current, ...keys])] : current.filter(k => !keys.includes(k)));
  }

  // ── 共享页签：筛选与勾选 ──

  setShareType(type: LibraryContentType): void {
    this.shareType = type;
    this.candidateChecked.set([]);
    void this.loadCandidates();
  }

  setShareKeyword(keyword: string): void {
    this.shareKeyword = keyword;
    void this.loadCandidates();
  }

  setShareLibraryFilter(libraryId: string): void {
    this.shareLibraryFilter = libraryId || '';
  }

  isCandidateChecked(row: CandidateRow): boolean {
    return this.candidateChecked().includes(this.rowKey(row.type, row.id));
  }

  toggleCandidate(row: CandidateRow, checked: boolean): void {
    const key = this.rowKey(row.type, row.id);
    const current = this.candidateChecked();
    this.candidateChecked.set(
      checked ? [...new Set([...current, key])] : current.filter(k => k !== key),
    );
  }

  allCandidatesChecked(): boolean {
    const rows = this.filteredCandidates();
    return rows.length > 0 && rows.every(r => this.isCandidateChecked(r));
  }

  toggleAllCandidates(checked: boolean): void {
    const keys = this.filteredCandidates().map(r => this.rowKey(r.type, r.id));
    const current = this.candidateChecked();
    this.candidateChecked.set(
      checked ? [...new Set([...current, ...keys])] : current.filter(k => !keys.includes(k)),
    );
  }

  /**
   * 加载可共享的候选内容（本校资源 / 课程 / 实训），并按两条规则过滤：
   * 1. **已关联到本库**的内容不再重复出现（它们在「库内容」页签里可见）；
   * 2. **只挂在本库可选范围之外**的库（如他校私有库）上的内容不列出 ——
   *    可选范围与文件资源表单一致（资源库 facets），避免出现「多出来的」内容。
   */
  async loadCandidates(): Promise<void> {
    // 先确保「可选资源库」已加载（过滤规则依赖它）
    if (!this.facetsLoaded) {
      await this.loadFilterLibraries();
    }
    const keyword = this.shareKeyword.trim() || undefined;
    this.candidatesLoading.set(true);
    const done = (rows: CandidateRow[]) => {
      const attached = this.attachedKeys();
      const allowed = this.allowedLibraryIds();
      const list = rows
        .filter(r => !attached.has(this.rowKey(r.type, r.id)))
        .filter(r => r.libraryIds.every(id => allowed.has(id)));
      this.candidates.set(list);
      // 清理已不在候选集中的勾选
      const keys = new Set(list.map(r => this.rowKey(r.type, r.id)));
      this.candidateChecked.set(this.candidateChecked().filter(k => keys.has(k)));
      this.candidatesLoading.set(false);
    };

    if (this.shareType === LibraryContentType.Course) {
      this.courseService.getList({ filter: keyword, skipCount: 0, maxResultCount: 100 } as any).subscribe({
        next: result =>
          done(
            (result.items || []).map(c => ({
              id: c.id as string,
              type: LibraryContentType.Course,
              name: c.title || '未命名课程',
              extra: c.semester || '',
              majorName: c.majorName || '',
              libraryIds: [
                ...new Set([...(c.libraryIds || []), ...(c.libraryId ? [c.libraryId] : [])]),
              ],
              libraryNames: (c.libraries || []).map(l => l.name || '').filter(Boolean),
            })),
          ),
        error: () => done([]),
      });
      return;
    }

    if (this.shareType === LibraryContentType.Practicum) {
      this.practicumService.getList({ filter: keyword, skipCount: 0, maxResultCount: 100 }).subscribe({
        next: result =>
          done(
            (result.items || []).map(p => ({
              id: p.id,
              type: LibraryContentType.Practicum,
              name: p.title || '未命名实训',
              extra: p.className || '',
              majorName: p.major || '',
              libraryIds: [
                ...new Set([...(p.libraryIds || []), ...(p.libraryId ? [p.libraryId] : [])]),
              ],
              libraryNames: (p.libraries || []).map(l => l.name || '').filter(Boolean),
            })),
          ),
        error: () => done([]),
      });
      return;
    }

    this.resourceService.getFilteredList({ filter: keyword, skipCount: 0, maxResultCount: 100 }).subscribe({
      next: result =>
        done(
          (result.items || [])
            // 只能共享「本校」资源：他校资源即使可见（如共享给我校的）也无法入到本校库，
            // 后端会直接返回「资源不存在。」（V2 前端验收 P1-7）。
            .filter(r => this.isOwnTenant(r.tenantId, r.ownerTenantId))
            .map(r => ({
              id: r.id as string,
              type: LibraryContentType.Resource,
              name: r.name || '未命名资源',
              extra: r.fileExtension || '',
              majorName: r.majorName || '',
              libraryIds: [
                ...new Set([
                  ...((r.libraries || []).map(l => l.id as string).filter(Boolean) as string[]),
                  ...(r.libraryId ? [r.libraryId] : []),
                ]),
              ],
              libraryNames: (r.libraries || []).map(l => l.name || '').filter(Boolean),
            })),
        ),
      error: () => done([]),
    });
  }

  /** 资源是否属于当前登录租户（本校）。 */
  private isOwnTenant(tenantId?: string | null, ownerTenantId?: string | null): boolean {
    const current = this.currentTenantId();
    if (!current) {
      return true;
    }
    const owner = ownerTenantId || tenantId || '';
    return !owner || owner === current;
  }

  /** 当前登录租户 Id（宿主 / 未登录时为空）。 */
  private currentTenantId(): string {
    const tenant = this.configState.getDeep('currentTenant') as Record<string, unknown> | undefined;
    return ((tenant?.['id'] as string) || '').toLowerCase();
  }

  // ── 批量操作 ──

  /** 把勾选的内容共享进本库。 */
  attachSelected(): void {
    const lib = this.manageLibrary;
    if (!lib?.id) {
      return;
    }
    const rows = this.candidates().filter(r => this.candidateChecked().includes(this.rowKey(r.type, r.id)));
    if (rows.length === 0) {
      this.message.warning('请先勾选要共享的内容');
      return;
    }
    this.shareSubmitting.set(true);
    let ok = 0;
    let fail = 0;
    let index = 0;
    const next = () => {
      if (index >= rows.length) {
        this.shareSubmitting.set(false);
        this.candidateChecked.set([]);
        this.loadContents();
        this.reload();
        // V2.0：共享进库需要审核，文案明确告知「已提交审核」而不是「已入库」
        this.message.success(
          `已提交 ${ok} 项入库审核${fail ? `，${fail} 项失败` : ''}（院校初审${''}，共建库还需联盟终审）`,
        );
        return;
      }
      const row = rows[index++];
      this.libraryService.attach(lib.id!, { contentType: row.type, contentId: row.id }).subscribe({
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

  /** 把勾选的内容移出本库。 */
  detachSelected(): void {
    const lib = this.manageLibrary;
    if (!lib?.id) {
      return;
    }
    const items = this.contents().filter(
      c => c.canDetach && this.contentChecked().includes(this.contentKey(c)),
    );
    if (items.length === 0) {
      this.message.warning('请先勾选要移出资源库的内容');
      return;
    }
    this.shareSubmitting.set(true);
    let ok = 0;
    let fail = 0;
    let index = 0;
    const next = () => {
      if (index >= items.length) {
        this.shareSubmitting.set(false);
        this.contentChecked.set([]);
        this.loadContents();
        this.reload();
        this.message.success(`已移出 ${ok} 项${fail ? `，${fail} 项失败` : ''}`);
        return;
      }
      const item = items[index++];
      this.libraryService.detach(lib.id!, item.contentType!, item.id!).subscribe({
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

  /** 单条移出资源库。 */
  detachContent(item: TeachingLibraryContentDto): void {
    const lib = this.manageLibrary;
    if (!lib?.id || !item.id || !item.canDetach) {
      return;
    }
    this.libraryService.detach(lib.id, item.contentType!, item.id).subscribe({
      next: () => {
        this.message.success('已移出资源库');
        this.loadContents();
        this.reload();
      },
      error: err => this.message.error(err?.error?.error?.message || '移出资源库失败'),
    });
  }

  // ────────────────────────────── 内容跳转 ──────────────────────────────

  /**
   * 库内内容跳转：
   * - 课程：与卡片上的「课程」计数块一致 —— 直接跳到该库的课程列表（课程详情是学员视图，
   *   教师在列表页管理更顺手），不逐个打开课程；
   * - 实训：跳实训详情（教师自己的管理页，含任务 / 资料）；
   * - 资源：跳资源中心并打开资源抽屉。
   */
  contentLink(item: TeachingLibraryContentDto): string[] | null {
    switch (item.contentType) {
      case LibraryContentType.Course:
        return ['/learning/course-list'];
      case LibraryContentType.Practicum:
        return ['/practicum/project', item.id];
      case LibraryContentType.Resource:
        return ['/resources'];
      default:
        return null;
    }
  }

  contentLinkQuery(item: TeachingLibraryContentDto): Record<string, string> {
    if (item.contentType === LibraryContentType.Course) {
      return { libraryId: (this.manageLibrary?.id as string) ?? '' };
    }
    if (item.contentType === LibraryContentType.Resource) {
      return { resourceId: item.id as string };
    }
    return {};
  }

  /** 跳转文案：课程是「跳到课程列表」，其余是「打开」。 */
  contentLinkText(item: TeachingLibraryContentDto): string {
    return item.contentType === LibraryContentType.Course ? '课程列表' : '打开';
  }

  /** 内容类型图标。 */
  contentTypeIcon(type?: LibraryContentType): string {
    switch (type) {
      case LibraryContentType.Course:
        return 'read';
      case LibraryContentType.Practicum:
        return 'experiment';
      default:
        return 'file-text';
    }
  }

  /** 内容类型名称。 */
  contentTypeName(type?: LibraryContentType): string {
    switch (type) {
      case LibraryContentType.Course:
        return '课程';
      case LibraryContentType.Practicum:
        return '实训';
      default:
        return '文件资源';
    }
  }
}

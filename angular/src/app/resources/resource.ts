import {ListService, LocalizationPipe, PagedResultDto, PermissionDirective, LocalizationService, RestService, Rest} from '@abp/ng.core';
import {Component, OnInit, inject, signal, ViewChild} from '@angular/core';
import {ResourceService, ResourceDto, ResourceVersionDto, ResourceCategoryDto, CreateUpdateResourceCategoryDto, AuditResourceDto, CompleteUploadResultDto} from '../proxy/resources';
import {AllianceService} from '../proxy/application/alliance/alliance.service';
import {resourceTypeOptions, resourceStatusOptions} from '../proxy/resources/enums';
import {ChunkUploadService} from '../proxy/controllers';
import {FormGroup, FormBuilder, Validators, ReactiveFormsModule} from '@angular/forms';
import {AsyncPipe, DatePipe, CommonModule} from "@angular/common";
import {FormsModule} from "@angular/forms";
import {Confirmation, ConfirmationService} from "@abp/ng.theme.shared";
import {NzTableModule} from 'ng-zorro-antd/table';
import {NzCardModule} from 'ng-zorro-antd/card';
import {NzButtonModule} from 'ng-zorro-antd/button';
import {NzFormModule} from 'ng-zorro-antd/form';
import {NzInputModule} from 'ng-zorro-antd/input';
import {NzSelectModule} from 'ng-zorro-antd/select';
import {NzModalModule} from 'ng-zorro-antd/modal';
import {NzIconModule} from 'ng-zorro-antd/icon';
import {NzPaginationModule} from 'ng-zorro-antd/pagination';
import {NzSpaceModule} from 'ng-zorro-antd/space';
import {NzTagModule} from 'ng-zorro-antd/tag';
import {NzDividerModule} from 'ng-zorro-antd/divider';
import {NzTimelineModule} from 'ng-zorro-antd/timeline';
import {NzTooltipModule} from 'ng-zorro-antd/tooltip';
import {NzEmptyModule} from 'ng-zorro-antd/empty';
import {NzSpinModule} from 'ng-zorro-antd/spin';
import {NzProgressModule} from 'ng-zorro-antd/progress';
import {NzTabsModule} from 'ng-zorro-antd/tabs';
import {NzStepsModule} from 'ng-zorro-antd/steps';
import {NzTreeModule} from 'ng-zorro-antd/tree';
import {NzTreeSelectModule} from 'ng-zorro-antd/tree-select';
import {NzInputNumberModule} from 'ng-zorro-antd/input-number';
import {NzUploadModule} from 'ng-zorro-antd/upload';
import {NzCollapseModule} from 'ng-zorro-antd/collapse';
import {NzCheckboxModule} from 'ng-zorro-antd/checkbox';
import {NzDrawerModule} from 'ng-zorro-antd/drawer';
import {NzMessageService} from 'ng-zorro-antd/message';
import {FilePreviewComponent} from '../shared/preview/file-preview.component';

@Component({
  selector: 'app-resource',
  templateUrl: './resource.html',
  styleUrls: ['./resource.scss'],
  providers: [ListService],
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    LocalizationPipe,
    DatePipe,
    ReactiveFormsModule,
    PermissionDirective,
    NzTableModule,
    NzCardModule,
    NzSpaceModule,
    NzButtonModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzModalModule,
    NzIconModule,
    NzPaginationModule,
    NzTagModule,
    NzDividerModule,
    NzTimelineModule,
    NzTooltipModule,
    NzEmptyModule,
    NzSpinModule,
    NzProgressModule,
    NzTabsModule,
    NzStepsModule,
    NzTreeModule,
    NzTreeSelectModule,
    NzInputNumberModule,
    NzUploadModule,
    NzCheckboxModule,
    NzDrawerModule,
    FilePreviewComponent
  ]
})
export class ResourceComponent implements OnInit {
  resources = {items: [], totalCount: 0} as PagedResultDto<ResourceDto>;
  selectedResource = signal<ResourceDto>({} as ResourceDto);
  versions = signal<ResourceVersionDto[]>([]);
  categories = signal<ResourceCategoryDto[]>([]);
  categoryTreeNodes = signal<any[]>([]);
  flatCategories = signal<any[]>([]);
  isCollected = signal<boolean>(false);

  form!: FormGroup;
  categoryForm!: FormGroup;
  resourceTypes = resourceTypeOptions;
  resourceStatuses = resourceStatusOptions;

  isModalOpen = false;
  isCategoryModalOpen = false;
  isLoading = signal(false);

  pageIndex = 1;
  pageSize = 10;

  selectedTabIndex = 0;
  selectedCategory: ResourceCategoryDto | null = null;
  leftPanelVisible = signal(true);
  selectedCategoryId = signal<string | null>(null);
  selectedCategoryName = signal<string>('');
  selectedTreeKeys = signal<string[]>([]);
  drawerVisible = signal(false);

  pendingAudits = signal<ResourceDto[]>([]);
  selectedAuditResource: ResourceDto | null = null;
  auditComment = '';
  isAuditModalOpen = false;
  isAuditLoading = signal(false);

  physicalDeleteRequests = signal<any[]>([]);
  selectedDeleteRequest: any = null;
  deleteReason = '';
  isDeleteModalOpen = false;
  isDeleteLoading = signal(false);

  CHUNK_SIZE = 1024 * 1024;
  selectedFile: File | null = null;
  selectedFileList: any[] = [];
  uploadProgress = signal(0);
  isUploading = signal(false);
  isSaving = signal(false);
  uploadId = '';
  uploadedFileInfo: CompleteUploadResultDto | null = null;

  versionUploadProgress = signal(0);
  isVersionUploading = signal(false);
  versionUploadedFileInfo: CompleteUploadResultDto | null = null;
  versionUpdateContent = '';

  isPreviewOpen = signal(false);
  @ViewChild('filePreview') filePreview!: FilePreviewComponent;

  public readonly list = inject(ListService);
  private readonly resourceService = inject(ResourceService);
  private readonly allianceService = inject(AllianceService);
  private readonly chunkUploadService = inject(ChunkUploadService);
  private readonly restService = inject(RestService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly localization = inject(LocalizationService);
  private readonly message = inject(NzMessageService);

  private readonly resourceTypeNames: Record<string, Record<number, string>> = {
    'zh-Hans': { 0: '文档', 1: '视频', 2: '音频', 3: '图片', 4: 'PPT' },
    'en': { 0: 'Document', 1: 'Video', 2: 'Audio', 3: 'Image', 4: 'PPT' }
  };

  private readonly resourceStatusNames: Record<string, Record<number, string>> = {
    'zh-Hans': { 0: '草稿', 1: '待审核', 2: '院校审核通过', 3: '联盟审核通过', 4: '审核拒绝', 5: '已隐藏' },
    'en': { 0: 'Draft', 1: 'Pending Review', 2: 'School Approved', 3: 'League Approved', 4: 'Rejected', 5: 'Hidden' }
  };

  l(key: string): string {
    return this.localization.instant('::' + key);
  }

  ngOnInit() {
    this.buildForm();
    this.loadCategories();
    this.loadResources();
  }

  loadResources() {
    this.resourceService.getFilteredList({
      maxResultCount: this.pageSize,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      categoryId: this.selectedCategoryId()
    }).subscribe((response) => {
      this.resources = response;
    });
  }

  buildForm() {
    const res = this.selectedResource();
    this.form = this.fb.group({
      name: [res.name || '', Validators.required],
      description: [res.description || ''],
      resourceType: [res.resourceType ?? null, Validators.required],
      categoryId: [res.categoryId ?? null],
      keywords: [res.keywords || ''],
      copyrightInfo: [res.copyrightInfo || ''],
      isDownloadable: [res.isDownloadable ?? true],
    });
  }

  loadCategories() {
    this.resourceService.getCategories().subscribe({
      next: (result) => {
        this.categories.set(result);
        this.categoryTreeNodes.set(this.buildTreeNodes(result));
        this.flatCategories.set(this.flattenWithLevel(result));
      }
    });
  }

  flattenWithLevel(categories: ResourceCategoryDto[], level = 0): any[] {
    const result: any[] = [];
    for (const cat of categories) {
      result.push({ ...cat, _level: level });
      if (cat.children && cat.children.length > 0) {
        result.push(...this.flattenWithLevel(cat.children, level + 1));
      }
    }
    return result;
  }

  buildTreeNodes(categories: ResourceCategoryDto[]): any[] {
    return categories.map(cat => ({
      title: cat.name,
      key: cat.id,
      icon: 'folder',
      isLeaf: !cat.children || cat.children.length === 0,
      expanded: true,
      origin: cat,
      children: cat.children && cat.children.length > 0 ? this.buildTreeNodes(cat.children) : []
    }));
  }

  flattenCategories(categories: ResourceCategoryDto[]): ResourceCategoryDto[] {
    const result: ResourceCategoryDto[] = [];
    for (const cat of categories) {
      result.push(cat);
      if (cat.children && cat.children.length > 0) {
        result.push(...this.flattenCategories(cat.children));
      }
    }
    return result;
  }

  findCategoryById(id: string): ResourceCategoryDto | undefined {
    return this.flattenCategories(this.categories()).find(c => c.id === id);
  }

  buildCategoryForm() {
    this.categoryForm = this.fb.group({
      name: [this.selectedCategory?.name || '', Validators.required],
      parentId: [this.selectedCategory?.parentId ?? null],
    });
  }

  createCategory() {
    this.selectedCategory = null;
    this.buildCategoryForm();
    this.isCategoryModalOpen = true;
  }

  editCategory(category: ResourceCategoryDto) {
    this.selectedCategory = category;
    this.buildCategoryForm();
    this.isCategoryModalOpen = true;
  }

  saveCategory() {
    if (this.categoryForm.invalid) {
      return;
    }

    const formValue = this.categoryForm.value;
    const input = {
      name: formValue.name,
      parentId: formValue.parentId,
      code: '',
      sortOrder: this.selectedCategory?.sortOrder ?? 0,
      isActive: this.selectedCategory?.isActive ?? true,
    } as CreateUpdateResourceCategoryDto;

    const request = this.selectedCategory?.id
      ? this.resourceService.updateCategory(this.selectedCategory.id, input)
      : this.resourceService.createCategory(input);

    request.subscribe({
      next: () => {
        this.isCategoryModalOpen = false;
        this.categoryForm.reset();
        this.loadCategories();
        this.message.success(this.l('SaveSuccess'));
      },
      error: () => {
        this.message.error(this.l('SaveFailed'));
      }
    });
  }

  deleteCategory(id: string) {
    this.confirmation.warn(this.l('DeleteCategoryConfirm'), this.l('Tip')).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.resourceService.deleteCategory(id).subscribe({
          next: () => {
            this.loadCategories();
            this.message.success(this.l('DeleteCategorySuccess'));
          },
          error: () => {
            this.message.error(this.l('DeleteCategoryFailed'));
          }
        });
      }
    });
  }

  moveCategoryUp(category: ResourceCategoryDto) {
    this.reorderCategory(category, -1);
  }

  moveCategoryDown(category: ResourceCategoryDto) {
    this.reorderCategory(category, 1);
  }

  private getSiblings(category: ResourceCategoryDto): ResourceCategoryDto[] {
    const all = this.categories();
    if (category.parentId) {
      const findChildren = (cats: ResourceCategoryDto[]): ResourceCategoryDto[] | null => {
        for (const cat of cats) {
          if (cat.id === category.parentId) return cat.children || [];
          if (cat.children) {
            const found = findChildren(cat.children);
            if (found) return found;
          }
        }
        return null;
      };
      return findChildren(all) || [];
    }
    return all;
  }

  private reorderCategory(category: ResourceCategoryDto, direction: number) {
    const siblings = this.getSiblings(category);
    const idx = siblings.findIndex(s => s.id === category.id);
    if (idx < 0) return;
    const newIdx = idx + direction;
    if (newIdx < 0 || newIdx >= siblings.length) return;

    const swapWith = siblings[newIdx];
    const input1 = { name: category.name, parentId: category.parentId, code: category.code || '', sortOrder: swapWith.sortOrder, isActive: category.isActive } as CreateUpdateResourceCategoryDto;
    const input2 = { name: swapWith.name, parentId: swapWith.parentId, code: swapWith.code || '', sortOrder: category.sortOrder, isActive: swapWith.isActive } as CreateUpdateResourceCategoryDto;

    this.resourceService.updateCategory(category.id!, input1).subscribe({
      next: () => {
        this.resourceService.updateCategory(swapWith.id!, input2).subscribe({
          next: () => this.loadCategories(),
          error: () => this.message.error(this.l('OperationFailed'))
        });
      },
      error: () => this.message.error(this.l('OperationFailed'))
    });
  }

  loadPendingAudits() {
    this.resourceService.getPendingAuditList({
      maxResultCount: this.pageSize,
      skipCount: (this.pageIndex - 1) * this.pageSize
    }).subscribe({
      next: (result) => {
        this.pendingAudits.set(result.items);
      }
    });
  }

  submitForReview(resourceId: string) {
    this.resourceService.submitForReview(resourceId).subscribe({
      next: () => {
        this.message.success(this.l('SubmitReviewSuccess'));
        this.loadResources();
      },
      error: () => {
        this.message.error(this.l('SubmitReviewFailed'));
      }
    });
  }

  openAuditModal(resource: ResourceDto) {
    this.selectedAuditResource = resource;
    this.auditComment = '';
    this.isAuditModalOpen = true;
  }

  auditResource(status: number) {
    if (!this.selectedAuditResource) return;

    this.isAuditLoading.set(true);

    // PendingReview(1) → 校审核; SchoolApproved(2) → 联盟审核
    const isSchoolAudit = this.selectedAuditResource.status === 1;

    if (isSchoolAudit) {
      this.resourceService.audit({
        resourceId: this.selectedAuditResource.id,
        status: status,
        comment: this.auditComment
      }).subscribe({
        next: () => {
          this.message.success(status === 1 ? this.l('AuditPassed') : this.l('AuditRejected'));
          this.isAuditModalOpen = false;
          this.isAuditLoading.set(false);
          this.loadPendingAudits();
          this.loadResources();
        },
        error: () => {
          this.message.error(this.l('AuditFailed'));
          this.isAuditLoading.set(false);
        }
      });
    } else {
      // 联盟审核：调用 AllianceService
      this.allianceService.leagueAudit({
        resourceId: this.selectedAuditResource.id,
        status: status,
        comment: this.auditComment
      }).subscribe({
        next: () => {
          this.message.success(status === 1 ? this.l('AuditPassed') : this.l('AuditRejected'));
          this.isAuditModalOpen = false;
          this.isAuditLoading.set(false);
          this.loadPendingAudits();
          this.loadResources();
        },
        error: () => {
          this.message.error(this.l('AuditFailed'));
          this.isAuditLoading.set(false);
        }
      });
    }
  }

  loadPhysicalDeleteRequests() {
    this.resourceService.getPendingPhysicalDeleteRequests({
      maxResultCount: this.pageSize,
      skipCount: (this.pageIndex - 1) * this.pageSize
    }).subscribe({
      next: (result) => {
        this.physicalDeleteRequests.set(result.items);
      }
    });
  }

  requestPhysicalDelete(resourceId: string, reason: string) {
    this.resourceService.requestPhysicalDelete({
      resourceId: resourceId,
      reason: reason
    }).subscribe({
      next: () => {
        this.message.success(this.l('DeleteRequestSubmitted'));
        this.isDeleteModalOpen = false;
        this.deleteReason = '';
        this.loadPhysicalDeleteRequests();
      },
      error: () => {
        this.message.error(this.l('SubmitFailed'));
      }
    });
  }

  openDeleteModal() {
    this.deleteReason = '';
    this.isDeleteModalOpen = true;
  }

  openDeleteModalForResource(resource: ResourceDto) {
    this.selectedResource.set(resource);
    this.deleteReason = '';
    this.isDeleteModalOpen = true;
  }

  submitDeleteRequest() {
    if (!this.selectedResource()?.id || !this.deleteReason.trim()) {
      return;
    }
    this.requestPhysicalDelete(this.selectedResource()!.id!, this.deleteReason);
  }

  approvePhysicalDelete(id: string) {
    this.resourceService.approvePhysicalDelete(id).subscribe({
      next: () => {
        this.message.success(this.l('DeleteApproved'));
        this.loadPhysicalDeleteRequests();
        this.loadResources();
      },
      error: () => {
        this.message.error(this.l('OperationFailed'));
      }
    });
  }

  rejectPhysicalDelete(id: string) {
    this.resourceService.rejectPhysicalDelete(id).subscribe({
      next: () => {
        this.message.success(this.l('DeleteRejected'));
        this.loadPhysicalDeleteRequests();
      },
      error: () => {
        this.message.error(this.l('OperationFailed'));
      }
    });
  }

  selectResource(resource: ResourceDto) {
    // Always create a new object reference to ensure signal triggers change detection
    this.selectedResource.set({ ...resource });
    this.drawerVisible.set(true);
    this.loadVersions(resource.id!);
    this.checkCollected(resource.id!);
  }

  closeDrawer() {
    this.drawerVisible.set(false);
  }

  onTabChange(index: number) {
    this.selectedTabIndex = index;
    if (index === 2) {
      this.loadPendingAudits();
    } else if (index === 3) {
      this.loadPhysicalDeleteRequests();
    }
  }

  onCategoryClick(event: any) {
    const node = event.node;
    if (this.selectedCategoryId() === node.key) {
      this.clearCategoryFilter();
      return;
    }
    this.selectedCategoryId.set(node.key);
    this.selectedCategoryName.set(node.title);
    this.selectedTreeKeys.set([node.key]);
    this.pageIndex = 1;
    this.loadResources();
  }

  clearCategoryFilter() {
    this.selectedCategoryId.set(null);
    this.selectedCategoryName.set('');
    this.selectedTreeKeys.set([]);
    this.pageIndex = 1;
    this.loadResources();
  }

  getResourceIcon(type?: number): string {
    switch (type) {
      case 0: return 'file-text';
      case 1: return 'video-camera';
      case 2: return 'audio';
      case 3: return 'picture';
      case 4: return 'file-ppt';
      default: return 'file';
    }
  }

  loadVersions(resourceId: string) {
    this.resourceService.getVersions(resourceId).subscribe({
      next: (result) => {
        this.versions.set(result);
      },
      error: (err) => {
        console.error('Load versions error:', err);
        this.versions.set([]);
      }
    });
  }

  checkCollected(resourceId: string) {
    this.resourceService.isCollected(resourceId).subscribe({
      next: (result) => {
        this.isCollected.set(result);
      },
      error: (err) => {
        console.error('Check collected error:', err);
        this.isCollected.set(false);
      }
    });
  }

  getResourceTypeName(value?: number): string {
    if (value === undefined || value === null) return '';
    const lang = this.localization.currentLang || 'en';
    return this.resourceTypeNames[lang]?.[value] ?? this.localization.instant('Enum:ResourceType.' + value);
  }

  getStatusName(value?: number): string {
    if (value === undefined || value === null) return '';
    const lang = this.localization.currentLang || 'en';
    return this.resourceStatusNames[lang]?.[value] ?? this.localization.instant('Enum:ResourceStatus.' + value);
  }

  getStatusColor(value?: number): string {
    switch (value) {
      case 0: return 'default';
      case 1: return 'processing';
      case 2: return 'warning';
      case 3: return 'success';
      case 4: return 'error';
      case 5: return 'default';
      default: return 'default';
    }
  }

  getAuditModalTitle(): string {
    if (!this.selectedAuditResource) return this.l('Audit');
    return this.selectedAuditResource.status === 1
      ? this.l('SchoolAudit')
      : this.l('LeagueAudit');
  }

  createResource() {
    this.selectedResource.set({} as ResourceDto);
    this.selectedFile = null;
    this.selectedFileList = [];
    this.uploadedFileInfo = null;
    this.uploadProgress.set(0);
    this.isSaving.set(false);
    this.buildForm();
    this.isModalOpen = true;
  }

  editResource(id: string) {
    this.resourceService.get(id).subscribe((resource) => {
      this.selectedResource.set(resource);
      this.buildForm();
      this.isModalOpen = true;
    });
  }

  save() {
    if (this.form.invalid) {
      return;
    }

    const formValue = { ...this.form.value };
    const res = this.selectedResource();

    // Edit mode: just update metadata
    if (res.id) {
      this.resourceService.update(res.id, formValue).subscribe(() => {
        this.isModalOpen = false;
        this.form.reset();
        this.loadResources();
        this.selectedResource.set({} as ResourceDto);
      });
      return;
    }

    // Create mode: must have a file selected
    if (!this.selectedFile) {
      this.message.warning(this.l('SelectFileFirst'));
      return;
    }

    this.isSaving.set(true);

    // Upload file first, then create resource
    this.uploadChunkedForCreate().then(() => {
      if (!this.uploadedFileInfo) {
        this.isSaving.set(false);
        return;
      }

      formValue.filePath = this.uploadedFileInfo.filePath;
      formValue.fileSize = this.uploadedFileInfo.fileSize;
      formValue.fileExtension = this.uploadedFileInfo.fileExtension;
      formValue.originalFileName = this.uploadedFileInfo.originalFileName;

      this.resourceService.create(formValue).subscribe({
        next: () => {
          this.isModalOpen = false;
          const createdCategoryId = formValue.categoryId;
          this.form.reset();
          this.uploadedFileInfo = null;
          this.selectedFile = null;
          this.selectedFileList = [];
          this.isSaving.set(false);

          if (createdCategoryId) {
            this.selectedCategoryId.set(createdCategoryId);
            const category = this.findCategoryById(createdCategoryId);
            this.selectedCategoryName.set(category?.name || '');
            this.selectedTreeKeys.set([createdCategoryId]);
            this.pageIndex = 1;
            this.selectedTabIndex = 0;
          } else {
            this.clearCategoryFilter();
          }
          this.loadResources();
          this.selectedResource.set({} as ResourceDto);
        },
        error: () => {
          this.isSaving.set(false);
          this.message.error(this.l('SaveFailed'));
        }
      });
    }).catch(() => {
      this.isSaving.set(false);
    });
  }

  delete(id: string) {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.resourceService.delete(id).subscribe(() => this.list.get());
      }
    });
  }

  toggleCollect() {
    const res = this.selectedResource();
    if (!res.id) return;

    if (this.isCollected()) {
      this.resourceService.uncollect(res.id).subscribe({
        next: () => {
          this.isCollected.set(false);
          this.list.get();
        },
        error: (err) => {
          console.error('Uncollect error:', err);
          this.message.error(this.l('OperationFailed'));
        }
      });
    } else {
      this.resourceService.collect(res.id).subscribe({
        next: () => {
          this.isCollected.set(true);
          this.list.get();
        },
        error: (err) => {
          console.error('Collect error:', err);
          this.message.error(this.l('OperationFailed'));
        }
      });
    }
  }

  download() {
    const res = this.selectedResource();
    if (!res.id || !res.isDownloadable) return;

    this.resourceService.download(res.id).subscribe({
      next: (data: any) => {
        let bytes: Uint8Array;
        if (typeof data === 'string') {
          const binary = atob(data);
          bytes = new Uint8Array(binary.length);
          for (let i = 0; i < binary.length; i++) {
            bytes[i] = binary.charCodeAt(i);
          }
        } else {
          bytes = new Uint8Array(data);
        }
        const blob = new Blob([new Uint8Array(bytes) as any], { type: 'application/octet-stream' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = res.originalFileName || 'download';
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        console.error('Download error:', err);
        this.message.error(this.l('DownloadFailed'));
      }
    });
  }

  previewFile() {
    const res = this.selectedResource();
    if (!res.id || !res.filePath) return;
    this.filePreview.open(
      res.id,
      res.originalFileName || res.name,
      res.fileExtension || '',
      res.fileSize || 0
    );
  }

  rollbackVersion(versionId: string) {
    this.confirmation.warn(this.l('RollbackConfirm'), this.l('Tip')).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.resourceService.rollbackVersion(versionId).subscribe({
          next: () => {
            const res = this.selectedResource();
            this.loadVersions(res.id!);
            this.list.get();
          }
        });
      }
    });
  }

  onPageIndexChange(index: number): void {
    this.pageIndex = index;
    this.loadResources();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.pageIndex = 1;
    this.loadResources();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.selectedFile = input.files[0];
    }
  }

  // Allowed file types based on resource type
  private readonly DOCUMENT_EXTENSIONS = ['.pdf', '.docx', '.ppt', '.pptx', '.xlsx', '.xls', '.doc'];
  private readonly VIDEO_EXTENSIONS = ['.mp4', '.mov', '.qt', '.avi', '.mkv', '.wmv', '.flv', '.webm'];

  getAllowedFileTypes(): string {
    const resourceType = this.form?.get('resourceType')?.value;
    if (resourceType === 1) {
      return this.VIDEO_EXTENSIONS.join(',');
    }
    return this.DOCUMENT_EXTENSIONS.join(',');
  }

  getResourceTypeHint(): string {
    const resourceType = this.form?.get('resourceType')?.value;
    if (resourceType === 1) {
      return '支持的视频格式：MP4, MOV, QT, AVI, MKV, WMV, FLV, WebM';
    }
    return '支持的文档格式：PDF, Word (DOC/DOCX), PowerPoint (PPT/PPTX), Excel (XLS/XLSX)';
  }

  private isFileTypeAllowed(fileName: string): boolean {
    const resourceType = this.form?.get('resourceType')?.value;
    const ext = '.' + fileName.split('.').pop()?.toLowerCase();
    if (resourceType === 1) {
      return this.VIDEO_EXTENSIONS.includes(ext);
    }
    return this.DOCUMENT_EXTENSIONS.includes(ext);
  }

  onResourceTypeChange(_value: number): void {
    // Clear selected file if its type doesn't match the new resource type
    if (this.selectedFile) {
      if (!this.isFileTypeAllowed(this.selectedFile.name)) {
        this.selectedFile = null;
        this.selectedFileList = [];
      }
    }
  }

  beforeUploadFile = (file: any): boolean => {
    const fileName = file.name || '';
    if (!this.isFileTypeAllowed(fileName)) {
      this.message.error(this.getResourceTypeHint());
      return false;
    }
    // Extract native File object from NzUploadFile
    const nativeFile: File = file.originFileObj || file;
    this.selectedFile = nativeFile instanceof File ? nativeFile : file;
    this.selectedFileList = [{
      uid: '-1',
      name: fileName,
      size: file.size,
      status: 'done'
    }];
    // Auto-fill resource name from file name if empty
    const currentName = this.form.get('name')?.value;
    if (!currentName) {
      const nameWithoutExt = fileName.replace(/\.[^/.]+$/, '');
      this.form.patchValue({ name: nameWithoutExt });
    }
    return false;
  };

  onFileListChange(fileList: any[]): void {
    if (fileList.length === 0) {
      this.selectedFile = null;
      this.selectedFileList = [];
    }
  }

  async uploadChunkedForCreate(): Promise<void> {
    if (!this.selectedFile) {
      return;
    }

    this.isUploading.set(true);
    this.uploadProgress.set(0);

    try {
      const initiateResult = await this.chunkUploadService.initiateUploadByInput({
        fileName: this.selectedFile.name,
        totalSize: this.selectedFile.size,
        chunkSize: this.CHUNK_SIZE
      }).toPromise();

      if (!initiateResult) {
        throw new Error('Failed to initiate upload');
      }

      this.uploadId = initiateResult.uploadId!;
      const totalChunks = initiateResult.totalChunks!;

      for (let i = 0; i < totalChunks; i++) {
        const start = i * this.CHUNK_SIZE;
        const end = Math.min(start + this.CHUNK_SIZE, this.selectedFile!.size);
        const chunk = this.selectedFile!.slice(start, end);

        const formData = new FormData();
        formData.append('file', chunk, this.selectedFile!.name);
        formData.append('uploadId', this.uploadId);
        formData.append('fileName', this.selectedFile!.name);
        formData.append('chunkNumber', i.toString());

        await this.restService.request<any, boolean>({
          method: 'POST',
          url: '/api/app/chunk-upload/upload',
          body: formData,
        }).toPromise();
        this.uploadProgress.set(Math.round(((i + 1) / totalChunks) * 100));
      }

      const completeResult = await this.chunkUploadService.completeUploadByInput({
        uploadId: this.uploadId,
        fileName: this.selectedFile.name,
        totalChunks: totalChunks
      }).toPromise();

      if (completeResult) {
        this.uploadedFileInfo = completeResult;
      }
    } catch (error) {
      this.message.error(this.l('UploadFailed'));
      console.error(error);
      throw error;
    } finally {
      this.isUploading.set(false);
    }
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }

  beforeUploadVersion(file: File): boolean {
    this.versionUploadedFileInfo = null;
    this.versionUploadProgress.set(0);
    this.uploadVersionFile(file);
    return false;
  }

  async uploadVersionFile(file: File): Promise<void> {
    this.isVersionUploading.set(true);
    this.versionUploadProgress.set(0);

    try {
      const initiateResult = await this.chunkUploadService.initiateUploadByInput({
        fileName: file.name,
        totalSize: file.size,
        chunkSize: this.CHUNK_SIZE
      }).toPromise();

      if (!initiateResult) {
        throw new Error('Failed to initiate upload');
      }

      const uploadId = initiateResult.uploadId!;
      const totalChunks = initiateResult.totalChunks!;

      for (let i = 0; i < totalChunks; i++) {
        const start = i * this.CHUNK_SIZE;
        const end = Math.min(start + this.CHUNK_SIZE, file.size);
        const chunk = file.slice(start, end);

        const formData = new FormData();
        formData.append('file', chunk, file.name);
        formData.append('uploadId', uploadId);
        formData.append('fileName', file.name);
        formData.append('chunkNumber', i.toString());

        await this.restService.request<any, boolean>({
          method: 'POST',
          url: '/api/app/chunk-upload/upload',
          body: formData,
        }).toPromise();
        this.versionUploadProgress.set(Math.round(((i + 1) / totalChunks) * 100));
      }

      const completeResult = await this.chunkUploadService.completeUploadByInput({
        uploadId: uploadId,
        fileName: file.name,
        totalChunks: totalChunks
      }).toPromise();

      if (completeResult) {
        this.versionUploadedFileInfo = completeResult;
        this.message.success(this.l('UploadSuccess'));
      }
    } catch (error) {
      this.message.error(this.l('UploadFailed'));
      console.error(error);
    } finally {
      this.isVersionUploading.set(false);
    }
  }

  uploadNewVersion(): void {
    if (!this.versionUploadedFileInfo) return;
    const res = this.selectedResource();
    if (!res.id) return;

    this.resourceService.uploadVersion({
      resourceId: res.id,
      filePath: this.versionUploadedFileInfo.filePath,
      fileSize: this.versionUploadedFileInfo.fileSize,
      fileExtension: this.versionUploadedFileInfo.fileExtension,
      originalFileName: this.versionUploadedFileInfo.originalFileName
    }).subscribe({
      next: () => {
        this.message.success(this.l('UploadVersionSuccess'));
        this.versionUploadedFileInfo = null;
        this.versionUploadProgress.set(0);
        this.loadVersions(res.id!);
        this.list.get();
        this.resourceService.get(res.id).subscribe((updated) => {
          this.selectedResource.set(updated);
        });
      },
      error: () => {
        this.message.error(this.l('UploadVersionFailed'));
      }
    });
  }
}

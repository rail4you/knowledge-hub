import {ListService, LocalizationPipe, PagedResultDto, PermissionDirective, LocalizationService, RestService, Rest} from '@abp/ng.core';
import {Component, OnInit, inject, signal} from '@angular/core';
import {ResourceService, ResourceDto, ResourceVersionDto, ResourceCategoryDto, CreateUpdateResourceCategoryDto, AuditResourceDto, CompleteUploadResultDto} from '../proxy/resources';
import {resourceTypeOptions, resourceStatusOptions} from '../proxy/resources/enums';
import {ChunkUploadService} from '../proxy/controllers';
import {FormGroup, FormBuilder, Validators, ReactiveFormsModule} from '@angular/forms';
import {AsyncPipe, CurrencyPipe, DatePipe, CommonModule} from "@angular/common";
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
import {NzMessageService} from 'ng-zorro-antd/message';
import {NzTabsModule} from 'ng-zorro-antd/tabs';
import {NzTreeModule} from 'ng-zorro-antd/tree';
import {NzTreeSelectModule} from 'ng-zorro-antd/tree-select';
import {NzInputNumberModule} from 'ng-zorro-antd/input-number';
import {NzUploadModule} from 'ng-zorro-antd/upload';
import {NzCollapseModule} from 'ng-zorro-antd/collapse';
import {NzCheckboxModule} from 'ng-zorro-antd/checkbox';

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
    CurrencyPipe,
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
    NzTreeModule,
    NzTreeSelectModule,
    NzInputNumberModule,
    NzUploadModule,
    NzCheckboxModule
  ]
})
export class ResourceComponent implements OnInit {
  resources = {items: [], totalCount: 0} as PagedResultDto<ResourceDto>;
  selectedResource = {} as ResourceDto;
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
  rightPanelVisible = signal(true);
  selectedCategoryId = signal<string | null>(null);
  selectedCategoryName = signal<string>('');
  selectedTreeKeys = signal<string[]>([]);
  
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
  uploadProgress = signal(0);
  isUploading = signal(false);
  uploadId = '';
  uploadedFileInfo: CompleteUploadResultDto | null = null;

  versionUploadProgress = signal(0);
  isVersionUploading = signal(false);
  versionUploadedFileInfo: CompleteUploadResultDto | null = null;
  versionUpdateContent = '';

  public readonly list = inject(ListService);
  private readonly resourceService = inject(ResourceService);
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
    this.form = this.fb.group({
      name: [this.selectedResource.name || '', Validators.required],
      description: [this.selectedResource.description || ''],
      resourceType: [this.selectedResource.resourceType ?? null, Validators.required],
      categoryId: [this.selectedResource.categoryId ?? null],
      keywords: [this.selectedResource.keywords || ''],
      copyrightInfo: [this.selectedResource.copyrightInfo || ''],
      isDownloadable: [this.selectedResource.isDownloadable ?? true],
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
        this.list.get();
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
        this.list.get();
      },
      error: () => {
        this.message.error(this.l('AuditFailed'));
        this.isAuditLoading.set(false);
      }
    });
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
      },
      error: () => {
        this.message.error(this.l('SubmitFailed'));
      }
    });
  }

  openDeleteModal() {
    this.selectedResource = this.selectedResource;
    this.deleteReason = '';
    this.isDeleteModalOpen = true;
  }

  approvePhysicalDelete(id: string) {
    this.resourceService.approvePhysicalDelete(id).subscribe({
      next: () => {
        this.message.success(this.l('DeleteApproved'));
        this.loadPhysicalDeleteRequests();
        this.list.get();
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
    this.selectedResource = resource;
    this.loadVersions(resource.id!);
    this.checkCollected(resource.id!);
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
    this.selectedResource = {} as ResourceDto;
    this.selectedFile = null;
    this.uploadedFileInfo = null;
    this.uploadProgress.set(0);
    this.buildForm();
    this.isModalOpen = true;
  }

  editResource(id: string) {
    this.resourceService.get(id).subscribe((resource) => {
      this.selectedResource = resource;
      this.buildForm();
      this.isModalOpen = true;
    });
  }

  save() {
    if (this.form.invalid) {
      return;
    }

    const formValue = { ...this.form.value };
    
    if (!this.selectedResource.id && this.uploadedFileInfo) {
      formValue.filePath = this.uploadedFileInfo.filePath;
      formValue.fileSize = this.uploadedFileInfo.fileSize;
      formValue.fileExtension = this.uploadedFileInfo.fileExtension;
      formValue.originalFileName = this.uploadedFileInfo.originalFileName;
    }

    const request = this.selectedResource.id
      ? this.resourceService.update(this.selectedResource.id, formValue)
      : this.resourceService.create(formValue);

    request.subscribe(() => {
      this.isModalOpen = false;
      const createdCategoryId = formValue.categoryId;
      this.form.reset();
      this.uploadedFileInfo = null;
      this.selectedFile = null;

      if (!this.selectedResource.id && createdCategoryId) {
        // 新建资源：跳转到对应分类的资源列表
        this.selectedCategoryId.set(createdCategoryId);
        const category = this.findCategoryById(createdCategoryId);
        this.selectedCategoryName.set(category?.name || '');
        this.selectedTreeKeys.set([createdCategoryId]);
        this.pageIndex = 1;
        this.selectedTabIndex = 0;
      } else if (!this.selectedResource.id) {
        // 新建资源但没设分类：跳转到全部资源
        this.clearCategoryFilter();
      } else {
        // 编辑资源：刷新当前列表
      }
      this.loadResources();
      this.selectedResource = {} as ResourceDto;
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
    if (!this.selectedResource.id) return;
    
    if (this.isCollected()) {
      this.resourceService.uncollect(this.selectedResource.id).subscribe({
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
      this.resourceService.collect(this.selectedResource.id).subscribe({
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
    if (!this.selectedResource.id || !this.selectedResource.isDownloadable) return;

    this.resourceService.download(this.selectedResource.id).subscribe({
      next: (data: any) => {
        let bytes: Uint8Array;
        if (typeof data === 'string') {
          // Backend returns Base64 encoded string
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
        a.download = this.selectedResource.originalFileName || 'download';
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: (err) => {
        console.error('Download error:', err);
        this.message.error(this.l('DownloadFailed'));
      }
    });
  }

  rollbackVersion(versionId: string) {
    this.confirmation.warn(this.l('RollbackConfirm'), this.l('Tip')).subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.resourceService.rollbackVersion(versionId).subscribe({
          next: () => {
            this.loadVersions(this.selectedResource.id!);
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

  async uploadChunked(): Promise<void> {
    if (!this.selectedFile) {
      this.message.warning(this.l('SelectFileFirst'));
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
        this.message.success(this.l('UploadSuccess'));
        this.form.patchValue({
          name: this.form.value.name || this.selectedFile!.name
        });
      }
    } catch (error) {
      this.message.error(this.l('UploadFailed'));
      console.error(error);
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
    if (!this.versionUploadedFileInfo || !this.selectedResource.id) {
      return;
    }

    this.resourceService.uploadVersion({
      resourceId: this.selectedResource.id,
      filePath: this.versionUploadedFileInfo.filePath,
      fileSize: this.versionUploadedFileInfo.fileSize,
      fileExtension: this.versionUploadedFileInfo.fileExtension,
      originalFileName: this.versionUploadedFileInfo.originalFileName
    }).subscribe({
      next: () => {
        this.message.success(this.l('UploadVersionSuccess'));
        this.versionUploadedFileInfo = null;
        this.versionUploadProgress.set(0);
        this.loadVersions(this.selectedResource.id!);
        this.list.get();
        this.resourceService.get(this.selectedResource.id).subscribe((resource) => {
          this.selectedResource = resource;
        });
      },
      error: () => {
        this.message.error(this.l('UploadVersionFailed'));
      }
    });
  }
}

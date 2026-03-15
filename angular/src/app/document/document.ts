import {ListService, LocalizationPipe, PagedResultDto, PermissionDirective, LocalizationService} from '@abp/ng.core';
import {Component, OnInit, inject, signal} from '@angular/core';
import {DocumentService, DocumentDto, documentTypeOptions, UserLookupDto} from '../proxy/documents';
import {FormGroup, FormBuilder, Validators, ReactiveFormsModule} from '@angular/forms';
import {AsyncPipe, CurrencyPipe, DatePipe, CommonModule} from "@angular/common";
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
import {NzDatePickerModule} from 'ng-zorro-antd/date-picker';
import {NzInputNumberModule} from 'ng-zorro-antd/input-number';
import {NzSpaceModule} from 'ng-zorro-antd/space';

@Component({
  selector: 'app-document',
  templateUrl: './document.html',
  styleUrls: ['./document.scss'],
  providers: [ListService],
  imports: [
    CommonModule,
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
    NzDatePickerModule,
    NzInputNumberModule
  ]
})
export class DocumentComponent implements OnInit {
  document = {items: [], totalCount: 0} as PagedResultDto<DocumentDto>;

  selectedDocument = {} as DocumentDto;
  users = signal<UserLookupDto[]>([]);

  form!: FormGroup;

  documentTypes = documentTypeOptions;

  isModalOpen = false;

  pageIndex = 1;
  pageSize = 10;

  public readonly list = inject(ListService);
  private readonly documentService = inject(DocumentService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly localization = inject(LocalizationService);

  ngOnInit() {
    this.buildForm();
    this.documentService.getUserLookup().subscribe({
      next: (result) => {
        this.users.set(result.items);
      },
      error: (err) => {
        console.error('Error loading users:', err);
      }
    });

    const documentStreamCreator = (query) => this.documentService.getList({...query, maxResultCount: this.pageSize, skipCount: (this.pageIndex - 1) * this.pageSize});

    this.list.hookToQuery(documentStreamCreator).subscribe((response) => {
      this.document = response;
    });
  }

  getDocumentTypeName(value: number): string {
    return this.localization.instant('Enum:DocumentType.' + value);
  }

  createDocument() {
    this.selectedDocument = {} as DocumentDto;
    this.buildForm();
    this.isModalOpen = true;
  }

  editDocument(id: string) {
    this.documentService.get(id).subscribe((document) => {
      this.selectedDocument = document;
      this.buildForm();
      this.isModalOpen = true;
    });
  }

  buildForm() {
    this.form = this.fb.group({
      name: [this.selectedDocument.name || '', Validators.required],
      type: [this.selectedDocument.type ?? null, Validators.required],
      userId: [this.selectedDocument.userId ?? null, Validators.required],
      publishDate: [
        this.selectedDocument.publishDate ? new Date(this.selectedDocument.publishDate) : null,
        Validators.required,
      ],
      price: [this.selectedDocument.price ?? null, Validators.required],
    });
  }

  save() {
    if (this.form.invalid) {
      return;
    }

    const formValue = {...this.form.value};
    if (formValue.publishDate) {
      formValue.publishDate = formValue.publishDate.toISOString();
    }

    const request = this.selectedDocument.id
      ? this.documentService.update(this.selectedDocument.id, formValue)
      : this.documentService.create(formValue);

    request.subscribe(() => {
      this.isModalOpen = false;
      this.form.reset();
      this.list.get();
    });
  }

  delete(id: string) {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure').subscribe((status) => {
      if (status === Confirmation.Status.confirm) {
        this.documentService.delete(id).subscribe(() => this.list.get());
      }
    });
  }

  onPageIndexChange(index: number): void {
    this.pageIndex = index;
    this.list.get();
  }

  onPageSizeChange(size: number): void {
    this.pageSize = size;
    this.pageIndex = 1;
    this.list.get();
  }
}

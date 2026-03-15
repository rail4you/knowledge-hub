import {ListService, LocalizationPipe, PagedResultDto, PermissionDirective, LocalizationService} from '@abp/ng.core';
import { Component, OnInit, inject, signal } from '@angular/core';
import {DocumentService, DocumentDto, documentTypeOptions, UserLookupDto} from '../proxy/documents';
import {FormGroup, FormBuilder, Validators, ReactiveFormsModule} from '@angular/forms';
import {
  NgbDateNativeAdapter,
  NgbDateAdapter,
  NgbDropdown,
  NgbDropdownToggle,
  NgbDropdownMenu, NgbDropdownItem, NgbInputDatepicker
} from '@ng-bootstrap/ng-bootstrap';
import {DataTableColumnCellDirective, DataTableColumnDirective, DatatableComponent} from "@swimlane/ngx-datatable";
import {AsyncPipe, CurrencyPipe, DatePipe} from "@angular/common";
import {
  Confirmation,
  ConfirmationService,
  ModalCloseDirective,
  ModalComponent, NgxDatatableDefaultDirective,
  NgxDatatableListDirective
} from "@abp/ng.theme.shared";

@Component({
  selector: 'app-document',
  templateUrl: './document.html',
  styleUrls: ['./document.scss'],
  providers: [ListService, {provide: NgbDateAdapter, useClass: NgbDateNativeAdapter}],
  imports: [
    LocalizationPipe,
    DatatableComponent,
    DataTableColumnDirective,
    CurrencyPipe,
    DatePipe,
    NgxDatatableListDirective,
    DataTableColumnCellDirective,
    NgbDropdown,
    NgbDropdownToggle,
    NgbDropdownMenu,
    NgbDropdownItem,
    ModalComponent,
    ReactiveFormsModule,
    NgbInputDatepicker,
    ModalCloseDirective,
    PermissionDirective,
    NgxDatatableDefaultDirective,
    AsyncPipe
  ]
})
export class DocumentComponent implements OnInit {
  document = { items: [], totalCount: 0 } as PagedResultDto<DocumentDto>;

  selectedDocument = {} as DocumentDto;
  users = signal<UserLookupDto[]>([]);

  form: FormGroup;

  documentTypes = documentTypeOptions;

  isModalOpen = false;

  public readonly list = inject(ListService);
  private readonly documentService = inject(DocumentService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);
  private readonly localization = inject(LocalizationService);

  ngOnInit() {
    this.documentService.getUserLookup().subscribe({
      next: (result) => {
        this.users.set(result.items);
      },
      error: (err) => {
        console.error('Error loading users:', err);
      }
    });

    const documentStreamCreator = (query) => this.documentService.getList(query);

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

    const request = this.selectedDocument.id
      ? this.documentService.update(this.selectedDocument.id, this.form.value)
      : this.documentService.create(this.form.value);

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
}

import {Component, OnInit, inject} from '@angular/core';
import {ListService, LocalizationPipe, PagedResultDto, PermissionDirective} from '@abp/ng.core';
import {UserService, UserDto} from '../proxy/users';
import {FormGroup, FormBuilder, Validators, ReactiveFormsModule} from '@angular/forms';
import {
  NgbDateNativeAdapter,
  NgbDateAdapter,
  NgbInputDatepicker,
  NgbDropdown, NgbDropdownItem,
  NgbDropdownToggle, NgbDropdownMenu,
} from '@ng-bootstrap/ng-bootstrap';
import {
  ConfirmationService,
  Confirmation,
  ModalComponent,
  NgxDatatableListDirective,
  NgxDatatableDefaultDirective, ModalCloseDirective
} from '@abp/ng.theme.shared';
import {DatePipe} from "@angular/common";
import {DataTableColumnCellDirective, DataTableColumnDirective, DatatableComponent} from "@swimlane/ngx-datatable";

@Component({
  selector: 'app-user',
  templateUrl: './user.html',
  styleUrls: ['./user.scss'],
  providers: [ListService, {provide: NgbDateAdapter, useClass: NgbDateNativeAdapter}],
  imports: [
    DatePipe,
    ModalComponent,
    ReactiveFormsModule,
    NgbInputDatepicker,
    LocalizationPipe,
    DatatableComponent,
    DataTableColumnDirective,
    NgxDatatableListDirective,
    PermissionDirective,
    DataTableColumnCellDirective,
    NgbDropdown,
    NgbDropdownToggle,
    NgbDropdownMenu,
    NgbDropdownItem,
    NgxDatatableDefaultDirective,
    ModalCloseDirective
  ]
})
export class UserComponent implements OnInit {
  user = {items: [], totalCount: 0} as PagedResultDto<UserDto>;

  isModalOpen = false;

  form: FormGroup;

  selectedUser = {} as UserDto;

  public readonly list = inject(ListService);
  private readonly userService = inject(UserService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);

  ngOnInit(): void {
    const userStreamCreator = (query) => this.userService.getList(query);

    this.list.hookToQuery(userStreamCreator).subscribe((response) => {
      this.user = response;
    });
  }

  createUser() {
    this.selectedUser = {} as UserDto;
    this.buildForm();
    this.isModalOpen = true;
  }

  editUser(id: string) {
    this.userService.get(id).subscribe((user) => {
      this.selectedUser = user;
      this.buildForm();
      this.isModalOpen = true;
    });
  }

  buildForm() {
    this.form = this.fb.group({
      name: [this.selectedUser.name || '', Validators.required],
      birthDate: [
        this.selectedUser.birthDate ? new Date(this.selectedUser.birthDate) : null,
        Validators.required,
      ],
      shortBio: [
        this.selectedUser.shortBio ? this.selectedUser.shortBio : null,
        Validators.required,
      ],
    });
  }

  save() {
    if (this.form.invalid) {
      return;
    }

    if (this.selectedUser.id) {
      this.userService
        .update(this.selectedUser.id, this.form.value)
        .subscribe(() => {
          this.isModalOpen = false;
          this.form.reset();
          this.list.get();
        });
    } else {
      this.userService.create(this.form.value).subscribe(() => {
        this.isModalOpen = false;
        this.form.reset();
        this.list.get();
      });
    }
  }

  delete(id: string) {
    this.confirmation.warn('::AreYouSureToDelete', '::AreYouSure')
      .subscribe((status) => {
        if (status === Confirmation.Status.confirm) {
          this.userService.delete(id).subscribe(() => this.list.get());
        }
      });
  }
}

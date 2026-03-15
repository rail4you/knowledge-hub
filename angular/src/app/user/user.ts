import {Component, OnInit, inject} from '@angular/core';
import {ListService, LocalizationPipe, PagedResultDto, PermissionDirective} from '@abp/ng.core';
import {UserService, UserDto} from '../proxy/users';
import {FormGroup, FormBuilder, Validators, ReactiveFormsModule} from '@angular/forms';
import {ConfirmationService, Confirmation} from '@abp/ng.theme.shared';
import {DatePipe, CommonModule} from "@angular/common";
import {NzTableModule} from 'ng-zorro-antd/table';
import {NzCardModule} from 'ng-zorro-antd/card';
import {NzButtonModule} from 'ng-zorro-antd/button';
import {NzFormModule} from 'ng-zorro-antd/form';
import {NzInputModule} from 'ng-zorro-antd/input';
import {NzModalModule} from 'ng-zorro-antd/modal';
import {NzIconModule} from 'ng-zorro-antd/icon';
import {NzPaginationModule} from 'ng-zorro-antd/pagination';
import {NzDatePickerModule} from 'ng-zorro-antd/date-picker';
import {NzSpaceModule} from 'ng-zorro-antd/space';

@Component({
  selector: 'app-user',
  templateUrl: './user.html',
  styleUrls: ['./user.scss'],
  providers: [ListService],
  imports: [
    DatePipe,
    CommonModule,
    LocalizationPipe,
    ReactiveFormsModule,
    PermissionDirective,
    NzTableModule,
    NzCardModule,
    NzSpaceModule,
    NzButtonModule,
    NzFormModule,
    NzInputModule,
    NzModalModule,
    NzIconModule,
    NzPaginationModule,
    NzDatePickerModule
  ]
})
export class UserComponent implements OnInit {
  user = {items: [], totalCount: 0} as PagedResultDto<UserDto>;

  isModalOpen = false;

  form!: FormGroup;

  selectedUser = {} as UserDto;

  pageIndex = 1;
  pageSize = 10;

  public readonly list = inject(ListService);
  private readonly userService = inject(UserService);
  private readonly fb = inject(FormBuilder);
  private readonly confirmation = inject(ConfirmationService);

  ngOnInit(): void {
    this.buildForm();
    const userStreamCreator = (query) => this.userService.getList({...query, maxResultCount: this.pageSize, skipCount: (this.pageIndex - 1) * this.pageSize});

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
    if (!this.form || this.form.invalid) {
      return;
    }

    const formValue = {...this.form.value};
    if (formValue.birthDate) {
      formValue.birthDate = formValue.birthDate.toISOString();
    }

    if (this.selectedUser.id) {
      this.userService
        .update(this.selectedUser.id, formValue)
        .subscribe(() => {
          this.isModalOpen = false;
          this.form = undefined;
          this.list.get();
        });
    } else {
      this.userService.create(formValue).subscribe(() => {
        this.isModalOpen = false;
        this.form = undefined;
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

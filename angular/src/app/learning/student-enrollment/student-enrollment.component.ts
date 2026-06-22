import { Component, signal, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule, NzModalService } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzGridModule } from 'ng-zorro-antd/grid';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { CourseService } from '../../proxy/courses/course.service';
import { StudentCourseService } from '../../proxy/courses/student-course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { StudentCourseDto } from '../../proxy/courses/dtos/models';
import type { IdentityUserDto } from '../../proxy/volo/abp/identity/models';
import { StudentCourseStatus } from '../../proxy/learning/enums/student-course-status.enum';

interface TenantDto {
  id: string;
  name: string;
}

@Component({
  selector: 'app-student-enrollment',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzSpinModule,
    NzEmptyModule,
    NzTableModule,
    NzIconModule,
    NzModalModule,
    NzFormModule,
    NzGridModule,
    NzCheckboxModule,
    NzProgressModule,
    NzDividerModule,
  ],
  templateUrl: './student-enrollment.component.html',
  styleUrls: ['./student-enrollment.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentEnrollmentComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly studentCourseService = inject(StudentCourseService);
  private readonly restService = inject(RestService);
  private readonly configService = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly cdr = inject(ChangeDetectorRef);

  // Data
  courses = signal<CourseDto[]>([]);
  enrollments = signal<StudentCourseDto[]>([]);
  tenants = signal<TenantDto[]>([]);
  availableStudents = signal<IdentityUserDto[]>([]);
  selectedStudentIds: Set<string> = new Set();

  // Filters
  selectedCourseId: string | null = null;
  selectedTenantId: string | null = null;
  filterText = '';
  studentFilterText = '';

  // Loading states
  loading = signal(false);
  addModalVisible = false;
  addModalLoading = false;
  saving = false;

  // Pagination
  total = 0;
  pageIndex = 1;
  pageSize = 10;

  studentTotal = 0;
  studentPageIndex = 1;
  studentPageSize = 10;

  statusOptions = [
    { label: '已选课', value: StudentCourseStatus.Enrolled },
    { label: '学习中', value: StudentCourseStatus.InProgress },
    { label: '已完成', value: StudentCourseStatus.Completed },
    { label: '已退课', value: StudentCourseStatus.Dropped },
  ];

  selectedStatusFilter: StudentCourseStatus | null = null;

  ngOnInit() {
    this.loadCourses();
    if (this.isHost) {
      this.loadTenants();
    }
  }

  loadCourses() {
    this.courseService.getList({ maxResultCount: 1000, skipCount: 0 } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
        this.cdr.markForCheck();
      },
    });
  }

  loadTenants() {
    this.restService.request<any, TenantDto[]>({
      method: 'GET',
      url: '/api/public/tenants',
    }).subscribe({
      next: (tenants) => {
        this.tenants.set(tenants || []);
        this.cdr.markForCheck();
      },
    });
  }

  loadEnrollments() {
    if (!this.selectedCourseId) {
      this.enrollments.set([]);
      this.total = 0;
      return;
    }

    this.loading.set(true);
    this.studentCourseService.getPaged({
      courseId: this.selectedCourseId,
      tenantId: this.selectedTenantId || undefined,
      status: this.selectedStatusFilter != null ? this.selectedStatusFilter : undefined,
      filter: this.filterText || undefined,
      skipCount: (this.pageIndex - 1) * this.pageSize,
      maxResultCount: this.pageSize,
    } as any).subscribe({
      next: (result) => {
        this.enrollments.set(result.items || []);
        this.total = result.totalCount || 0;
        this.loading.set(false);
        this.cdr.markForCheck();
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  onCourseChange() {
    this.pageIndex = 1;
    this.loadEnrollments();
  }

  onTenantChange() {
    this.pageIndex = 1;
    this.loadEnrollments();
  }

  onStatusFilterChange() {
    this.pageIndex = 1;
    this.loadEnrollments();
  }

  onPageIndexChange(index: number) {
    this.pageIndex = index;
    this.loadEnrollments();
  }

  onPageSizeChange(size: number) {
    this.pageSize = size;
    this.pageIndex = 1;
    this.loadEnrollments();
  }

  getStatusLabel(status?: number): string {
    const labels: Record<number, string> = {
      0: '已选课',
      1: '学习中',
      2: '已完成',
      3: '已退课',
    };
    return labels[status ?? 0] || '未知';
  }

  getStatusColor(status?: number): string {
    const colors: Record<number, string> = {
      0: 'blue',
      1: 'processing',
      2: 'success',
      3: 'default',
    };
    return colors[status ?? 0] || 'default';
  }

  openAddModal() {
    if (!this.selectedCourseId) {
      this.message.warning('请先选择课程');
      return;
    }
    this.selectedStudentIds.clear();
    this.studentFilterText = '';
    this.studentPageIndex = 1;
    this.addModalVisible = true;
    this.loadAvailableStudents();
  }

  loadAvailableStudents() {
    this.addModalLoading = true;
    this.studentCourseService.getAvailableStudents({
      courseId: this.selectedCourseId!,
      tenantId: this.selectedTenantId || undefined,
      filter: this.studentFilterText || undefined,
      skipCount: (this.studentPageIndex - 1) * this.studentPageSize,
      maxResultCount: this.studentPageSize,
    } as any).subscribe({
      next: (result) => {
        this.availableStudents.set(result.items || []);
        this.studentTotal = result.totalCount || 0;
        this.addModalLoading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.addModalLoading = false;
      },
    });
  }

  onStudentPageChange(index: number) {
    this.studentPageIndex = index;
    this.loadAvailableStudents();
  }

  toggleStudentSelection(studentId: string) {
    if (this.selectedStudentIds.has(studentId)) {
      this.selectedStudentIds.delete(studentId);
    } else {
      this.selectedStudentIds.add(studentId);
    }
  }

  isStudentSelected(studentId: string): boolean {
    return this.selectedStudentIds.has(studentId);
  }

  selectAllStudents() {
    this.availableStudents().forEach(s => {
      if (s.id) this.selectedStudentIds.add(s.id);
    });
  }

  /** P1-10：跨页全选 — 后端一次性返回当前筛选下所有可选学生 ID */
  selectingAll = false;
  selectAllAcrossPages() {
    if (this.selectingAll) return;
    this.selectingAll = true;
    this.studentCourseService
      .getAllAvailableStudentIds({
        courseId: this.selectedCourseId!,
        tenantId: this.selectedTenantId || undefined,
        filter: this.studentFilterText || undefined,
      } as any)
      .subscribe({
        next: (ids) => {
          ids.forEach(id => this.selectedStudentIds.add(id as string));
          this.message.success(`已选中 ${ids.length} 人（跨页全选）`);
          this.selectingAll = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.message.error('跨页全选失败');
          this.selectingAll = false;
        },
      });
  }

  deselectAllStudents() {
    this.selectedStudentIds.clear();
  }

  handleAddModalOk() {
    if (this.selectedStudentIds.size === 0) {
      this.message.warning('请选择至少一个学生');
      return;
    }

    this.saving = true;
    this.studentCourseService.batchEnroll({
      studentIds: Array.from(this.selectedStudentIds),
      courseId: this.selectedCourseId!,
    }).subscribe({
      next: () => {
        this.saving = false;
        this.addModalVisible = false;
        this.message.success(`成功添加 ${this.selectedStudentIds.size} 个学生`);
        this.loadEnrollments();
      },
      error: () => {
        this.saving = false;
        this.message.error('添加学生失败');
      },
    });
  }

  handleAddModalCancel() {
    this.addModalVisible = false;
  }

  dropStudent(enrollment: StudentCourseDto) {
    this.modal.confirm({
      nzTitle: '确认退课',
      nzContent: `确定要让学生「${enrollment.studentName || ''}」退课吗？`,
      nzOkText: '确认退课',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        if (!enrollment.id) return;
        this.studentCourseService.delete(enrollment.id).subscribe({
          next: () => {
            this.message.success('退课成功');
            this.loadEnrollments();
          },
          error: () => {
            this.message.error('退课失败');
          },
        });
      },
    });
  }

  get isHost(): boolean {
    return !this.configService.getDeep('currentUser.tenantId');
  }
}

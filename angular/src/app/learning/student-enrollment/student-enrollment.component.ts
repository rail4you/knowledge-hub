import { Component, signal, inject, OnInit, ChangeDetectionStrategy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzInputNumberModule } from 'ng-zorro-antd/input-number';
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
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzTooltipModule } from 'ng-zorro-antd/tooltip';
import { NzPageHeaderModule } from 'ng-zorro-antd/page-header';
import { ConfigStateService, RestService } from '@abp/ng.core';
import { CourseService } from '../../proxy/courses/course.service';
import { StudentCourseService } from '../../proxy/courses/student-course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import type { StudentCourseDto } from '../../proxy/courses/dtos/models';
import type { TenantUserDto } from '../../proxy/application/identity/models';
import type { MajorLookupDto } from '../../proxy/majors/dtos/models';
import { MajorService } from '../../proxy/majors/major.service';
import { StudentCourseStatus } from '../../proxy/learning/enums/student-course-status.enum';
import { CourseStatus } from '../../proxy/courses/enums/course-status.enum';
import {
  TeachingClassDto,
  TeachingClassMemberDto,
  TeachingClassService,
} from './teaching-class.service';

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
    NzInputNumberModule,
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
    NzDividerModule,
    NzTooltipModule,
    NzPageHeaderModule,
  ],
  templateUrl: './student-enrollment.component.html',
  styleUrls: ['./student-enrollment.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentEnrollmentComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly studentCourseService = inject(StudentCourseService);
  private readonly majorService = inject(MajorService);
  private readonly restService = inject(RestService);
  private readonly configService = inject(ConfigStateService);
  private readonly message = inject(NzMessageService);
  private readonly modal = inject(NzModalService);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly teachingClassService = inject(TeachingClassService);

  // Data
  courses = signal<CourseDto[]>([]);
  enrollments = signal<StudentCourseDto[]>([]);
  tenants = signal<TenantDto[]>([]);
  majors = signal<MajorLookupDto[]>([]);
  availableStudents = signal<TenantUserDto[]>([]);
  selectedStudentIds: Set<string> = new Set();

  // ── 教学班 ────────────────────────────────────────────────
  /** 本校教学班（用于「按班级筛选可选学生」） */
  teachingClasses = signal<TeachingClassDto[]>([]);
  /** 当前按班级筛选；null = 不限班级 */
  selectedClassId: string | null = null;
  /** 班级管理弹窗 */
  classModalVisible = false;
  classModalLoading = false;
  classMembers = signal<TeachingClassMemberDto[]>([]);
  classMemberTotal = 0;
  /** 当前正在查看成员的班级 */
  activeClassId: string | null = null;

  /** 当前正在查看成员的班级实体 */
  activeClass(): TeachingClassDto | undefined {
    const id = this.activeClassId;
    if (!id) return undefined;
    return this.teachingClasses().find(c => c.id === id);
  }
  /** 新建班级表单 */
  newClassName = '';
  newClassMajorId: string | null = null;
  newClassGradeYear: number | null = null;
  newClassDescription = '';
  creatingClass = false;

  // Filters
  selectedCourseId: string | null = null;
  selectedTenantId: string | null = null;
  selectedMajorId: string | null = null;
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
    this.loadMajors();
    this.loadTeachingClasses();
    if (this.isHost) {
      this.loadTenants();
    }
  }

  /**
   * 教学班列表：用于「按班级筛选可选学生」。
   * 只取进行中的班级（草稿 / 已结课不用于选课筛选）。
   */
  loadTeachingClasses() {
    this.teachingClassService
      .getList({
        tenantId: this.isHost ? (this.selectedTenantId || null) : null,
        status: 1,
        skipCount: 0,
        maxResultCount: 200,
      })
      .subscribe({
        next: (result) => {
          this.teachingClasses.set(result?.items || []);
          this.cdr.markForCheck();
        },
        error: () => {
          // 班级功能不可用时不影响原有选课流程
          this.teachingClasses.set([]);
        },
      });
  }

  /** 切换班级筛选：只影响「可选学生」列表 */
  onClassFilterChange() {
    this.studentPageIndex = 1;
    this.loadAvailableStudents();
  }

  /** 打开班级管理 */
  openClassManager() {
    this.classModalVisible = true;
    this.loadTeachingClasses();
  }

  /** 新建教学班 */
  createClass() {
    const name = this.newClassName.trim();
    if (!name) {
      this.message.warning('请输入班级名称');
      return;
    }
    this.creatingClass = true;
    this.teachingClassService
      .create({
        name,
        majorId: this.newClassMajorId,
        gradeYear: this.newClassGradeYear,
        description: this.newClassDescription || null,
        status: 1,
      })
      .subscribe({
        next: () => {
          this.creatingClass = false;
          this.message.success('班级已创建');
          this.newClassName = '';
          this.newClassGradeYear = null;
          this.newClassDescription = '';
          this.loadTeachingClasses();
          this.cdr.markForCheck();
        },
        error: () => {
          this.creatingClass = false;
          this.message.error('创建班级失败');
        },
      });
  }

  /** 查看某个班的成员 */
  openClassMembers(cls: TeachingClassDto) {
    this.activeClassId = cls.id;
    this.classModalLoading = true;
    this.teachingClassService
      .getMembers({ classId: cls.id, skipCount: 0, maxResultCount: 200 })
      .subscribe({
        next: (result) => {
          this.classMembers.set(result?.items || []);
          this.classMemberTotal = result?.totalCount || 0;
          this.classModalLoading = false;
          this.cdr.markForCheck();
        },
        error: () => {
          this.classMembers.set([]);
          this.classModalLoading = false;
        },
      });
  }

  /** 把「添加学生」弹窗里当前已选的学生加入指定班级 */
  addSelectedStudentsToClass(cls: TeachingClassDto) {
    if (this.selectedStudentIds.size === 0) {
      this.message.warning('请先在「添加学生」里选中学生');
      return;
    }
    this.teachingClassService
      .addMembers(cls.id, { studentIds: Array.from(this.selectedStudentIds) })
      .subscribe({
        next: (added) => {
          this.message.success(
            added && added.length
              ? `已将 ${added.length} 名学生加入「${cls.name}」`
              : '所选学生均已在该班中'
          );
          if (this.activeClassId === cls.id) this.openClassMembers(cls);
        },
        error: () => this.message.error('加入班级失败'),
      });
  }

  /** 移出班级（不影响其选课与成绩记录） */
  removeClassMember(cls: TeachingClassDto | undefined, member: TeachingClassMemberDto) {
    if (!cls) return;
    this.modal.confirm({
      nzTitle: '移出班级',
      nzContent: `确定把「${member.studentName || member.userName || ''}」移出「${cls.name}」吗？该学生的选课与成绩记录会保留。`,
      nzOkText: '确认移出',
      nzOkDanger: true,
      nzCancelText: '取消',
      nzOnOk: () => {
        this.teachingClassService.removeMember(cls.id, member.studentId).subscribe({
          next: () => {
            this.message.success('已移出班级');
            this.openClassMembers(cls);
          },
          error: () => this.message.error('移出班级失败'),
        });
      },
    });
  }

  loadCourses() {
    // 租户隔离：租户管理员由后端 CurrentTenant 自动过滤；Host 根据选中的租户过滤，
    // 未选中则传 undefined 让 Host 看到所有租户课程（后端 tenantFilter 为 null 时不过滤 TenantId）。
    const tenantId = this.isHost ? (this.selectedTenantId || undefined) : undefined;
    this.courseService.getList({ maxResultCount: 1000, skipCount: 0, tenantId } as any).subscribe({
      next: (result) => {
        this.courses.set(result.items || []);
        this.cdr.markForCheck();
      },
    });
  }

  loadMajors() {
    this.majorService.getLookupList().subscribe({
      next: (majors) => {
        this.majors.set(majors || []);
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

  /** 课程是否已发布：只有已发布课程才能新选课（与后端闸门同口径） */
  isPublished(course: CourseDto | null | undefined): boolean {
    return course?.status === CourseStatus.Published;
  }

  /**
   * 课程下拉项文案：未发布课程标注状态并禁用，
   * 避免老师选到草稿课、提交后才被后端拒绝。
   */
  courseOptionLabel(course: CourseDto): string {
    const title = course.title || '未命名课程';
    return this.isPublished(course) ? title : `${title}（未发布，不能选课）`;
  }

  /** 当前选中课程是否已发布 */
  selectedCoursePublished(): boolean {
    if (!this.selectedCourseId) return false;
    return this.isPublished(this.courses().find(c => c.id === this.selectedCourseId));
  }

  onTenantChange() {
    this.pageIndex = 1;
    // Host 切换租户时，课程下拉也应按租户重新加载，避免看到其他租户的课程
    if (this.isHost) {
      this.selectedCourseId = null;
      this.enrollments.set([]);
      this.loadCourses();
    }
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
    // 只有已发布课程才能新选课：这里先拦一道，避免走到提交才被后端拒绝
    if (!this.selectedCoursePublished()) {
      this.message.warning('课程尚未发布，发布后才能为学生选课');
      return;
    }
    this.selectedStudentIds.clear();
    this.studentFilterText = '';
    this.selectedMajorId = null;
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
      majorId: this.selectedMajorId || undefined,
      // 按班级筛选：一个班的学生过滤出来即可多选后批量选科
      classId: this.selectedClassId || undefined,
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

  onMajorFilterChange() {
    this.studentPageIndex = 1;
    this.loadAvailableStudents();
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
        majorId: this.selectedMajorId || undefined,
        // 与列表使用同一套筛选条件（含班级），避免"全选匹配结果"选中范围与列表不一致
        classId: this.selectedClassId || undefined,
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
      // 记录选课来源班级（仅来源标记，不改变数据归属）
      classId: this.selectedClassId || undefined,
    } as any).subscribe({
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

  /**
   * 对已退课学生重新选课。复用 batchEnroll：
   * 后端看到该 student 在该课程下已有 Dropped 记录，会原地复活
   * （status=Enrolled、progress=0、enrolledAt=now），而非重复插入。
   */
  reEnrollStudent(enrollment: StudentCourseDto) {
    if (!enrollment.studentId || !enrollment.courseId) {
      this.message.warning('记录缺失学生或课程信息，无法重新选课');
      return;
    }
    this.modal.confirm({
      nzTitle: '重新选课',
      nzContent: `将「${enrollment.studentName || ''}」重新加入该课程？\n\n学习进度将清零。`,
      nzOkText: '确认选课',
      nzCancelText: '取消',
      nzOnOk: () => {
        this.studentCourseService.batchEnroll({
          studentIds: [enrollment.studentId!],
          courseId: enrollment.courseId!,
        }).subscribe({
          next: () => {
            this.message.success('已重新选课');
            this.loadEnrollments();
          },
          error: () => {
            this.message.error('重新选课失败');
          },
        });
      },
    });
  }

  get isHost(): boolean {
    return !this.configService.getDeep('currentUser.tenantId');
  }
}

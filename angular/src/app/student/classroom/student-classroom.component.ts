import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzMessageService } from 'ng-zorro-antd/message';
import { RouterModule } from '@angular/router';
import { CheckInService } from '../../proxy/classroom/check-in.service';
import { GroupTaskService } from '../../proxy/classroom/group-task.service';
import { ClassroomPracticeService } from '../../proxy/classroom/classroom-practice.service';
import type { MyCheckInRecordDto, GroupTaskDto, PracticeSessionDto } from '../../proxy/classroom/dtos/models';
import type { CourseDto } from '../../proxy/courses/dtos/models';
import { LearningService } from '../../proxy/learning/learning.service';
import { DiscussionBoardComponent } from '../../classroom/discussion/discussion-board.component';
import { GROUP_TASK_STATUS_LABEL, PRACTICE_STATUS_LABEL, STATUS_TAG_COLOR } from '../../classroom/classroom-labels';
import { GroupTaskStatus } from '../../proxy/classroom/enums/group-task-status.enum';

type StudentTab = 'checkIn' | 'groupTask' | 'practice' | 'discussion';

/**
 * 学生端「课堂互动」：一页四块（签到记录 / 小组任务 / 课堂练习 / 讨论答疑）。
 *
 * 统一进一个页面而不是四个菜单项：学生打开频率低、彼此强相关（都在"我这门课的课堂活动"里），
 * 分成四个侧边栏入口反而难找。讨论区复用教师端同一个组件（route data.studentMode = true）。
 */
@Component({
  selector: 'app-student-classroom',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzButtonModule,
    NzSelectModule,
    NzSpinModule,
    NzEmptyModule,
    NzIconModule,
    NzTableModule,
    NzTagModule,
    NzProgressModule,
    DiscussionBoardComponent,
  ],
  templateUrl: './student-classroom.component.html',
  styleUrls: ['./student-classroom.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentClassroomComponent implements OnInit {
  private readonly learningService = inject(LearningService);
  private readonly checkInService = inject(CheckInService);
  private readonly groupTaskService = inject(GroupTaskService);
  private readonly practiceService = inject(ClassroomPracticeService);
  private readonly message = inject(NzMessageService);

  readonly tab = signal<StudentTab>('checkIn');
  readonly courses = signal<CourseDto[]>([]);
  readonly selectedCourseId = signal<string | null>(null);

  readonly checkIns = signal<MyCheckInRecordDto[]>([]);
  readonly groupTasks = signal<GroupTaskDto[]>([]);
  readonly practices = signal<PracticeSessionDto[]>([]);
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loadCourses();
  }

  loadCourses(): void {
    this.learningService.getMyCourses().subscribe({
      next: list => {
        const mapped = (list ?? []).map(x => ({ id: x.courseId, title: x.courseTitle }) as CourseDto);
        this.courses.set(mapped);
        if (mapped.length === 1 && mapped[0].id) {
          this.onCourseSelected(mapped[0].id);
        }
      },
      error: () => this.courses.set([]),
    });
  }

  onCourseSelected(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
    this.loadAll();
  }

  setTab(tab: StudentTab): void {
    this.tab.set(tab);
    if (tab !== 'discussion') this.loadAll();
  }

  loadAll(): void {
    const courseId = this.selectedCourseId();
    if (!courseId) return;

    this.loading.set(true);
    this.checkInService.getMyRecords(courseId).subscribe({
      next: rows => this.checkIns.set(rows ?? []),
      error: () => this.checkIns.set([]),
    });

    this.groupTaskService.getMyTasks(courseId).subscribe({
      next: rows => this.groupTasks.set(rows ?? []),
      error: () => this.groupTasks.set([]),
    });

    this.practiceService.getMySessions(courseId).subscribe({
      next: rows => {
        this.practices.set(rows ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.practices.set([]);
        this.loading.set(false);
      },
    });
  }

  groupTaskStatus(task: GroupTaskDto): string {
    if ((task.status ?? 0) === GroupTaskStatus.Published && task.dueAt && new Date(task.dueAt) < new Date()) {
      return '已截止';
    }
    return GROUP_TASK_STATUS_LABEL[(task.status ?? 0) as GroupTaskStatus] ?? '—';
  }

  practiceStatus(session: PracticeSessionDto): string {
    return PRACTICE_STATUS_LABEL[(session.status ?? 0)] ?? '—';
  }

  tagColor(label: string): string {
    return STATUS_TAG_COLOR[label] ?? 'default';
  }

  percent(value?: number | null): number {
    return Math.round((value ?? 0) * 100);
  }
}

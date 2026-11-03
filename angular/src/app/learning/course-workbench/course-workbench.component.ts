import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { take } from 'rxjs';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';

/** 课程内容模块（一个入口对应一个已有页面） */
interface CourseModule {
  key: string;
  title: string;
  desc: string;
  icon: string;
  route: string;
  /** 是否需要先选章节（进入后再选） */
  needChapter?: boolean;
}

/**
 * 课程工作台：选一门课 → 进入它的各个内容模块。
 *
 * <p>背景：原来章节 / 习题 / 章节习题 / 课程资源 / 章节资源 / 教案 / 案例 / 试卷
 * 是 8 个平级侧边栏菜单，每个都是「先选课程 → 再操作」的同一套交互，
 * 平铺在菜单里既长又重复。这里收敛成一个入口，进去先选课，
 * 再按内容类型分发（带 courseId 直达，原页面自动选中该课程）。</p>
 */
@Component({
  selector: 'app-course-workbench',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzCardModule,
    NzEmptyModule,
    NzIconModule,
    NzInputModule,
    NzSelectModule,
    NzSpinModule,
    NzTagModule,
  ],
  templateUrl: './course-workbench.component.html',
  styleUrls: ['./course-workbench.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CourseWorkbenchComponent implements OnInit {
  private readonly courseService = inject(CourseService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly courses = signal<CourseDto[]>([]);
  readonly keyword = signal('');
  readonly selectedCourseId = signal<string | null>(null);

  readonly modules: CourseModule[] = [
    {
      key: 'chapter',
      title: '章节管理',
      desc: '课程目录树与章节增删改，支持多级嵌套',
      icon: 'deployment-unit',
      route: '/learning/chapter-management',
    },
    {
      key: 'exercise',
      title: '习题管理',
      desc: '题库维护：题目、选项、答案与解析',
      icon: 'form',
      route: '/learning/exercise-management',
    },
    {
      key: 'chapter-exercise',
      title: '章节习题',
      desc: '把题库挂到章节上，控制章节的学习顺序',
      icon: 'unordered-list',
      route: '/learning/chapter-exercise',
    },
    {
      key: 'course-resource',
      title: '课程资源',
      desc: '课程级的教学资源挂载',
      icon: 'folder-open',
      route: '/learning/course-resource',
    },
    {
      key: 'chapter-resource',
      title: '章节资源',
      desc: '章节级的资源挂载（进入后再选章节）',
      icon: 'file',
      route: '/learning/chapter-resource',
      needChapter: true,
    },
    {
      key: 'lesson-plan',
      title: '教案管理',
      desc: '按课程维护教案，支持 AI 生成',
      icon: 'signature',
      route: '/learning/lesson-plan',
    },
    {
      key: 'case',
      title: '案例库',
      desc: '课程案例的收集、审核与分享',
      icon: 'experiment',
      route: '/learning/course-case',
    },
    {
      key: 'exam',
      title: '试卷管理',
      desc: '组卷、题目抽取与试卷生成',
      icon: 'file-done',
      route: '/learning/exam-management',
    },
  ];

  readonly filteredCourses = computed(() => {
    const keyword = this.keyword().trim().toLowerCase();
    if (!keyword) {
      return this.courses();
    }
    return this.courses().filter(c => c.title?.toLowerCase().includes(keyword));
  });

  readonly selectedCourse = computed(() =>
    this.courses().find(c => c.id === this.selectedCourseId()) ?? null,
  );

  ngOnInit(): void {
    this.loadCourses();
    // 支持从课程详情带 courseId 进来：预选该课程
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId');
      if (courseId) {
        this.selectedCourseId.set(courseId);
      }
    });
  }

  loadCourses(): void {
    this.loading.set(true);
    this.courseService
      .getList({ maxResultCount: 200, skipCount: 0 } as any)
      .subscribe({
        next: result => {
          this.courses.set(result.items || []);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.message.error('课程列表加载失败');
        },
      });
  }

  selectCourse(courseId: string | null): void {
    this.selectedCourseId.set(courseId);
  }

  /** 进入内容模块：带 courseId 直达，原页面会自动选中该课程 */
  open(module: CourseModule): void {
    const courseId = this.selectedCourseId();
    if (!courseId) {
      this.message.warning('请先选择课程');
      return;
    }
    void this.router.navigate([module.route], { queryParams: { courseId } });
  }
}

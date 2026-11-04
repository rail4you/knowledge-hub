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
import { CourseContextService } from '../course-context.service';
import { CourseService } from '../../proxy/courses/course.service';
import type { CourseDto } from '../../proxy/courses/dtos/models';

/** 课程内容模块（一个入口对应一个已有页面） */
interface CourseModule {
  key: string;
  title: string;
  desc: string;
  icon: string;
  route: string;
  /** 卡片角标：标明它在所属分组里的角色（成对关系一眼可见） */
  badge: string;
  /** 是否需要先选章节（进入后再选） */
  needChapter?: boolean;
}

/**
 * 内容模块分组：按「结构 → 资源 → 题库 → 考核」四条规则组织，每组两张卡片成对出现。
 *
 * <p>分组规则：每组内部是同一类工作的「一对」——
 * 课程级对象与它的章节级对象、或生产对象与它的挂载对象；
 * 组与组之间是「先搭骨架 → 再填素材 → 再出题 → 最后考核」的先后顺序。</p>
 */
interface CourseModuleGroup {
  key: string;
  /** 组序号（01 / 02 / 03 / 04） */
  index: string;
  title: string;
  desc: string;
  modules: CourseModule[];
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
  private readonly courseContext = inject(CourseContextService);
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
      badge: '骨架',
      route: '/learning/chapter-management',
    },
    {
      key: 'exercise',
      title: '习题管理',
      desc: '题库维护：题目、选项、答案与解析',
      icon: 'form',
      badge: '题库',
      route: '/learning/exercise-management',
    },
    {
      key: 'chapter-exercise',
      title: '章节习题',
      desc: '把题库挂到章节上，控制章节的学习顺序',
      icon: 'unordered-list',
      badge: '章节',
      route: '/learning/chapter-exercise',
    },
    {
      key: 'course-resource',
      title: '课程资源',
      desc: '课程级的教学资源挂载，可预览、下载与移除',
      icon: 'folder-open',
      badge: '课程级',
      route: '/learning/course-resource',
    },
    {
      key: 'chapter-resource',
      title: '章节资源',
      desc: '章节级的资源挂载（进入后再选章节）',
      icon: 'file',
      badge: '章节级',
      route: '/learning/chapter-resource',
      needChapter: true,
    },
    {
      key: 'lesson-plan',
      title: '教案管理',
      desc: '按课程维护教案，支持 AI 生成',
      icon: 'signature',
      badge: '设计',
      route: '/learning/lesson-plan',
    },
    {
      key: 'case',
      title: '案例库',
      desc: '课程案例的收集、审核与分享',
      icon: 'experiment',
      badge: '素材',
      route: '/learning/course-case',
    },
    {
      key: 'exam',
      title: '试卷管理',
      desc: '组卷、题目抽取与试卷生成',
      icon: 'file-done',
      badge: '考核',
      route: '/learning/exam-management',
    },
  ];

  /**
   * 四个分组，每组两张卡片成对出现：
   * 01 结构与设计（章节 = 骨架，教案 = 教学设计）
   * 02 教学资源（课程级 ↔ 章节级）
   * 03 题库体系（题库 ↔ 挂到章节）
   * 04 案例与考核（案例素材 ↔ 组卷考试）
   */
  readonly groups: CourseModuleGroup[] = [
    {
      key: 'structure',
      index: '01',
      title: '结构与设计',
      desc: '先搭课程骨架，再写教学设计',
      modules: [
        this.modules.find(m => m.key === 'chapter')!,
        this.modules.find(m => m.key === 'lesson-plan')!,
      ],
    },
    {
      key: 'resource',
      index: '02',
      title: '教学资源',
      desc: '课程级与章节级资源成对维护',
      modules: [
        this.modules.find(m => m.key === 'course-resource')!,
        this.modules.find(m => m.key === 'chapter-resource')!,
      ],
    },
    {
      key: 'exercise',
      index: '03',
      title: '题库体系',
      desc: '先建题库，再挂到章节上',
      modules: [
        this.modules.find(m => m.key === 'exercise')!,
        this.modules.find(m => m.key === 'chapter-exercise')!,
      ],
    },
    {
      key: 'assessment',
      index: '04',
      title: '案例与考核',
      desc: '收集案例素材，用于组卷与考核',
      modules: [
        this.modules.find(m => m.key === 'case')!,
        this.modules.find(m => m.key === 'exam')!,
      ],
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
    // 优先级：URL 上的 courseId > 全局「当前课程」；两者都没有则留空
    this.route.queryParamMap.pipe(take(1)).subscribe(params => {
      const courseId = params.get('courseId') || this.courseContext.currentCourseId();
      if (courseId) {
        this.selectedCourseId.set(courseId);
        this.courseContext.setCourse(courseId);
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
    // 选中的课程写回全局，内容页之间跳转时保持一致
    this.courseContext.setCourse(courseId);
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

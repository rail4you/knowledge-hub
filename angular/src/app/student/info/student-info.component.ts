import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DestroyRef } from '@angular/core';
import { StudentEmptyComponent } from '../shared/student-empty/student-empty.component';
import {
  STUDENT_INFO_PAGES,
  STUDENT_INFO_TYPES,
  StudentInfoType,
} from './student-info-content';

/**
 * 学生端 · 信息页（关于我们 / 帮助中心 / 隐私政策）
 *
 * 页面按设计就是静态说明文本：文案统一放在
 * `student-info-content.ts`，本组件只负责按路由 type 取内容并渲染。
 * 若将来需要按租户配置文案，应改走后端接口，前端不再内嵌。
 */
@Component({
  selector: 'app-student-info',
  standalone: true,
  imports: [CommonModule, RouterModule, StudentEmptyComponent],
  templateUrl: './student-info.component.html',
  styleUrls: ['./student-info.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentInfoComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly type = signal<StudentInfoType | null>(null);
  readonly page = computed(() => {
    const t = this.type();
    return t ? STUDENT_INFO_PAGES[t] : null;
  });

  /** 页签（顺序固定） */
  readonly tabs = STUDENT_INFO_TYPES.map(key => ({ key, title: STUDENT_INFO_PAGES[key].title }));

  ngOnInit(): void {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const raw = params.get('type') ?? '';
      const key = (STUDENT_INFO_TYPES as string[]).includes(raw) ? (raw as StudentInfoType) : null;
      if (!key) {
        this.router.navigate(['/student']);
        return;
      }
      this.type.set(key);
    });
  }
}

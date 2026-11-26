import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '@abp/ng.core';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCheckboxModule } from 'ng-zorro-antd/checkbox';
import { NzResultModule } from 'ng-zorro-antd/result';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ClassroomTokenService } from '../../proxy/classroom/classroom-token.service';
import { CheckInService } from '../../proxy/classroom/check-in.service';
import { ClassroomPracticeService } from '../../proxy/classroom/classroom-practice.service';
import { GroupTaskService } from '../../proxy/classroom/group-task.service';
import type {
  ClassroomTokenSessionDto,
  PracticePaperDto,
  PracticeAttemptResultDto,
  GroupTaskDetailDto,
} from '../../proxy/classroom/dtos/models';
import { ClassroomTokenKind } from '../../proxy/classroom/enums/classroom-token-kind.enum';
import { STATUS_TAG_COLOR } from '../classroom-labels';

/** 判断题/选择题的作答方式（与后端 ExerciseType 对齐） */
const TYPE_SINGLE = 0;
const TYPE_MULTI = 1;
const TYPE_TRUE_FALSE = 2;
const TYPE_FILL = 3;

interface PracticeAnswerState {
  exerciseId: string;
  type: number;
  title: string;
  questionContent: string;
  options: string[];
  /** 单选/判断：'A' / 'true'；多选：'A,B'；填空：文本 */
  answer: string;
  multiSelected: string[];
}

/**
 * 免登录短链落地页（签到 / 课堂练习 / 小组任务三合一）。
 *
 * 三个路由共用本组件，靠 `route.data.kind` 区分：
 *   /check-in/:token              → CheckIn
 *   /classroom-practice/:token    → Practice
 *   /group-task/:token            → GroupTask
 *
 * 语义（对齐 kg-edu 的实现）：**看得到、看不了名单**；提交动作要求登录并回跳。
 * token 不含租户，后端按 token 反查所属学校，因此链接可跨校投屏、也不会被 __tenant cookie 污染。
 */
@Component({
  selector: 'app-classroom-token',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzButtonModule,
    NzInputModule,
    NzSpinModule,
    NzIconModule,
    NzTagModule,
    NzRadioModule,
    NzCheckboxModule,
    NzResultModule,
  ],
  templateUrl: './classroom-token.component.html',
  styleUrls: ['./classroom-token.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ClassroomTokenComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly tokenService = inject(ClassroomTokenService);
  private readonly checkInService = inject(CheckInService);
  private readonly practiceService = inject(ClassroomPracticeService);
  private readonly groupTaskService = inject(GroupTaskService);
  private readonly message = inject(NzMessageService);

  readonly kind = signal<ClassroomTokenKind>(ClassroomTokenKind.CheckIn);
  readonly token = signal('');
  readonly session = signal<ClassroomTokenSessionDto | null>(null);
  readonly loading = signal(true);
  readonly errorText = signal<string | null>(null);
  readonly busy = signal(false);

  readonly isAuthenticated = computed(() => this.authService.isAuthenticated);

  // 签到
  readonly checkInDone = signal(false);

  // 课堂练习
  readonly paper = signal<PracticePaperDto | null>(null);
  readonly answers = signal<PracticeAnswerState[]>([]);
  readonly result = signal<PracticeAttemptResultDto | null>(null);

  // 小组任务
  readonly task = signal<GroupTaskDetailDto | null>(null);
  readonly submissionContent = signal('');

  ngOnInit(): void {
    const kindRaw = this.route.snapshot.data['kind'];
    this.kind.set((typeof kindRaw === 'number' ? kindRaw : ClassroomTokenKind.CheckIn) as ClassroomTokenKind);
    this.token.set(this.route.snapshot.paramMap.get('token') ?? '');
    this.resolve();
  }

  resolve(): void {
    if (!this.token()) {
      this.loading.set(false);
      this.errorText.set('链接不完整');
      return;
    }

    this.loading.set(true);
    this.tokenService.resolve({ token: this.token(), kind: this.kind() }).subscribe({
      next: data => {
        this.session.set(data);
        this.loading.set(false);
        this.checkInDone.set(!!data.participated && this.kind() === ClassroomTokenKind.CheckIn);
        if (this.isAuthenticated()) this.loadDetail();
      },
      error: err => {
        this.loading.set(false);
        this.errorText.set(err?.error?.error?.message || '链接无效或已失效');
      },
    });
  }

  /** 登录后按类型加载各自的明细数据 */
  loadDetail(): void {
    if (!this.isAuthenticated()) return;

    if (this.kind() === ClassroomTokenKind.Practice) {
      this.loadPaper();
    } else if (this.kind() === ClassroomTokenKind.GroupTask) {
      this.loadTask();
    }
  }

  login(): void {
    const returnUrl = typeof window !== 'undefined' ? window.location.pathname : undefined;
    this.authService.navigateToLogin(returnUrl ? { returnUrl } : undefined);
  }

  // ── 签到 ─────────────────────────────────────────────────────

  doCheckIn(): void {
    if (!this.isAuthenticated()) {
      this.login();
      return;
    }

    this.busy.set(true);
    const finish = (lat?: number | null, lng?: number | null) => {
      this.checkInService.doCheckIn({ token: this.token(), latitude: lat ?? null, longitude: lng ?? null }).subscribe({
        next: res => {
          this.busy.set(false);
          this.checkInDone.set(true);
          this.message.success(res.isNew ? '签到成功' : '你之前已经签过了');
        },
        error: err => {
          this.busy.set(false);
          this.message.error(err?.error?.error?.message || '签到失败');
        },
      });
    };

    // 需要定位时先尝试取浏览器定位；失败则让后端给出明确提示
    if (typeof navigator !== 'undefined' && navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        pos => finish(pos.coords.latitude, pos.coords.longitude),
        () => finish(),
        { timeout: 8000, enableHighAccuracy: true },
      );
      return;
    }

    finish();
  }

  // ── 课堂练习 ─────────────────────────────────────────────────

  loadPaper(): void {
    this.practiceService.getPaper(this.token()).subscribe({
      next: paper => {
        this.paper.set(paper);
        this.answers.set((paper.items ?? []).map(item => this.toAnswerState(item)));
        this.result.set(paper.myAttempt ?? null);
      },
      error: err => this.message.error(err?.error?.error?.message || '题目加载失败'),
    });
  }

  private toAnswerState(item: {
    exerciseId?: string;
    type?: number;
    title?: string;
    questionContent?: string;
    options?: string | null;
  }): PracticeAnswerState {
    let options: string[] = [];
    if (item.options) {
      try {
        const parsed = JSON.parse(item.options);
        if (Array.isArray(parsed)) options = parsed.map(String);
      } catch {
        options = [];
      }
    }

    return {
      exerciseId: item.exerciseId ?? '',
      type: item.type ?? TYPE_SINGLE,
      title: item.title ?? '',
      questionContent: item.questionContent ?? '',
      options,
      answer: '',
      multiSelected: [],
    };
  }

  letters = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];

  setSingleAnswer(index: number, letter: string): void {
    this.patchAnswer(index, { answer: letter });
  }

  setTrueFalse(index: number, value: boolean): void {
    this.patchAnswer(index, { answer: value ? 'true' : 'false' });
  }

  toggleMulti(index: number, letter: string, checked: boolean): void {
    const current = this.answers()[index];
    const next = new Set(current.multiSelected);
    if (checked) next.add(letter);
    else next.delete(letter);
    const sorted = [...next].sort();
    this.patchAnswer(index, { multiSelected: sorted, answer: sorted.join(',') });
  }

  setFillAnswer(index: number, value: string): void {
    this.patchAnswer(index, { answer: value });
  }

  private patchAnswer(index: number, patch: Partial<PracticeAnswerState>): void {
    const list = [...this.answers()];
    if (!list[index]) return;
    list[index] = { ...list[index], ...patch };
    this.answers.set(list);
  }

  submitPractice(): void {
    if (!this.isAuthenticated()) {
      this.login();
      return;
    }

    this.busy.set(true);
    this.practiceService
      .submit({
        token: this.token(),
        answers: this.answers().map(a => ({ exerciseId: a.exerciseId, answer: a.answer })),
      })
      .subscribe({
        next: res => {
          this.busy.set(false);
          this.result.set(res);
          this.message.success('已提交');
        },
        error: err => {
          this.busy.set(false);
          this.message.error(err?.error?.error?.message || '提交失败');
        },
      });
  }

  // ── 小组任务 ─────────────────────────────────────────────────

  loadTask(): void {
    this.groupTaskService.getMyTask(this.token()).subscribe({
      next: task => {
        this.task.set(task);
        // 后端 token 解析已回传「我所在组名」，按组名反查我组的提交（不猜 current user 成员关系）
        const myGroupName = this.session()?.myGroupName;
        const myGroup = myGroupName ? (task.groups ?? []).find(g => g.name === myGroupName) : undefined;
        this.submissionContent.set(myGroup?.submission?.content ?? '');
      },
      error: err => this.message.error(err?.error?.error?.message || '任务加载失败'),
    });
  }

  submitGroupTask(): void {
    if (!this.isAuthenticated()) {
      this.login();
      return;
    }
    if (!this.submissionContent()?.trim()) {
      this.message.warning('请填写提交内容');
      return;
    }

    this.busy.set(true);
    this.groupTaskService.submit({ token: this.token(), content: this.submissionContent() }).subscribe({
      next: () => {
        this.busy.set(false);
        this.message.success('已提交');
        this.resolve();
        this.loadTask();
      },
      error: err => {
        this.busy.set(false);
        this.message.error(err?.error?.error?.message || '提交失败');
      },
    });
  }

  // ── 视图辅助 ─────────────────────────────────────────────────

  get kindTitle(): string {
    switch (this.kind()) {
      case ClassroomTokenKind.Practice:
        return '课堂练习';
      case ClassroomTokenKind.GroupTask:
        return '小组任务';
      default:
        return '课堂签到';
    }
  }

  statusColor(): string {
    const status = this.session()?.status ?? '';
    return STATUS_TAG_COLOR[status] ?? 'default';
  }

  answerResult(item: PracticeAttemptResultDto['answers'] extends (infer T)[] | undefined ? T : never): string {
    if (item.isCorrect === true) return '正确';
    if (item.isCorrect === false) return '错误';
    return '待批改';
  }

  isChoice(type: number): boolean {
    return type === TYPE_SINGLE || type === TYPE_MULTI;
  }

  isMulti(type: number): boolean {
    return type === TYPE_MULTI;
  }

  isTrueFalse(type: number): boolean {
    return type === TYPE_TRUE_FALSE;
  }

  isFill(type: number): boolean {
    return type === TYPE_FILL;
  }
}

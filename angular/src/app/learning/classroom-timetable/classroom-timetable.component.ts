import { Component, ChangeDetectionStrategy, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ClassroomTimetableService, Timetable, TimetableItem } from './classroom-timetable.service';

type ViewMode = 'day' | 'week' | 'month';

interface CalendarCell {
  date: Date;
  inCurrent: boolean;
  items: TimetableItem[];
  isToday: boolean;
}

/**
 * 互动课堂 · 课表。
 *
 * <p>数据源 = 教学智能体分配的任务（课堂智能体任务）。每个任务在课表上落两个点：
 * 「布置」（创建日）与「截止」（DueTime）。</p>
 *
 * <p>三视图：<b>日</b>（按小时时间轴）/ <b>周</b>（7 列网格）/ <b>月</b>（月历格）。
 * 悬浮任一课程块显示「任务名称 / 日期 / 时间段 / 学校」—— 采购演示项 ③ 的四个字段。</p>
 */
@Component({
  selector: 'app-classroom-timetable',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, NzButtonModule, NzIconModule, NzSpinModule, NzSelectModule, NzEmptyModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="tt-page">
      <header class="tt-head">
        <div>
          <h1>互动课堂 · 课表</h1>
          <p class="tt-sub">
            课表来自「教学智能体」分配的任务：每个任务按<b>布置日</b>与<b>截止日</b>落到日历上，
            鼠标悬浮查看任务名称、日期、时间段与学校。
          </p>
        </div>
        <div class="tt-head__right">
          <nz-select
            style="width:220px"
            nzPlaceHolder="全部课程"
            nzAllowClear
            [ngModel]="courseId()"
            (ngModelChange)="onCourseChange($event)"
          >
            @for (c of timetable()?.courses ?? []; track c.id) {
              <nz-option [nzValue]="c.id" [nzLabel]="c.name + '（' + c.taskCount + '）'"></nz-option>
            }
          </nz-select>
        </div>
      </header>

      <!-- 工具条：视图切换 + 导航 + 汇总 -->
      <div class="tt-toolbar">
        <div class="tt-switch">
          @for (v of views; track v.key) {
            <button type="button" class="tt-switch__btn" [class.on]="view() === v.key" (click)="setView(v.key)">
              {{ v.label }}
            </button>
          }
        </div>

        <button nz-button nzType="text" (click)="shift(-1)" nz-tooltip nzTooltipTitle="上一页">
          <span nz-icon nzType="left"></span>
        </button>
        <button nz-button nzType="text" (click)="goToday()">今天</button>
        <button nz-button nzType="text" (click)="shift(1)" nz-tooltip nzTooltipTitle="下一页">
          <span nz-icon nzType="right"></span>
        </button>

        <h2 class="tt-range">{{ rangeLabel() }}</h2>

        <div class="tt-stats">
          <span><b>{{ tt()?.taskCount ?? 0 }}</b> 个任务</span>
          <span><b>{{ tt()?.courseCount ?? 0 }}</b> 门课程</span>
          @if ((tt()?.overdueCount ?? 0) > 0) {
            <span class="is-warn"><b>{{ tt()!.overdueCount }}</b> 已逾期</span>
          }
        </div>
      </div>

      @if (loading()) {
        <div class="tt-loading"><nz-spin nzSimple nzTip="加载课表…"></nz-spin></div>
      } @else if ((tt()?.items?.length ?? 0) === 0) {
        <div class="tt-empty">
          <nz-empty nzNotFoundContent="这个时间段还没有智能体任务，去「教学智能体」给课程分配任务后会出现在这里"></nz-empty>
        </div>
      } @else {
        <!-- 日视图 -->
        @if (view() === 'day') {
          <div class="tt-day">
            @for (h of hours; track h) {
              <div class="tt-day__row">
                <span class="tt-day__hour">{{ h }}:00</span>
                <div class="tt-day__slot">
                  @for (i of itemsAtHour(h); track i.taskId + i.kind) {
                    <div class="tt-chip" [class.is-due]="i.kind === 'due'" [class.is-overdue]="i.isOverdue"
                         [title]="tip(i)">
                      <b>{{ i.title }}</b>
                      <span>{{ i.courseName }} · {{ timeOf(i.at) }}</span>
                    </div>
                  }
                </div>
              </div>
            }
          </div>
        }

        <!-- 周视图 -->
        @if (view() === 'week') {
          <div class="tt-week">
            @for (d of weekDays(); track d.date.getTime()) {
              <div class="tt-week__col" [class.is-today]="d.isToday">
                <header class="tt-week__head">
                  <b>{{ weekDayName(d.date) }}</b>
                  <span>{{ d.date.getMonth() + 1 }}-{{ d.date.getDate() }}</span>
                </header>
                <div class="tt-week__body">
                  @for (i of d.items; track i.taskId + i.kind) {
                    <div class="tt-chip tt-chip--block" [class.is-due]="i.kind === 'due'" [class.is-overdue]="i.isOverdue"
                         [title]="tip(i)">
                      <b>{{ i.title }}</b>
                      <span>{{ i.courseName }}</span>
                      <span class="tt-chip__meta">{{ i.kind === 'due' ? '截止' : '布置' }} {{ timeOf(i.at) }}</span>
                    </div>
                  }
                  @if (d.items.length === 0) { <div class="tt-week__empty">—</div> }
                </div>
              </div>
            }
          </div>
        }

        <!-- 月视图 -->
        @if (view() === 'month') {
          <div class="tt-month">
            <div class="tt-month__weekdays">
              @for (n of weekNames; track n) { <span>{{ n }}</span> }
            </div>
            <div class="tt-month__grid">
              @for (cell of monthCells(); track cell.date.getTime()) {
                <div class="tt-month__cell" [class.is-out]="!cell.inCurrent" [class.is-today]="cell.isToday">
                  <span class="tt-month__date">{{ cell.date.getDate() }}</span>
                  @for (i of cell.items.slice(0, 3); track i.taskId + i.kind) {
                    <div class="tt-dot" [class.is-due]="i.kind === 'due'" [class.is-overdue]="i.isOverdue"
                         [title]="tip(i)">
                      {{ i.title }}
                    </div>
                  }
                  @if (cell.items.length > 3) {
                    <span class="tt-month__more">+{{ cell.items.length - 3 }}</span>
                  }
                </div>
              }
            </div>
          </div>
        }
      }

      <p class="tt-legend">
        <span class="lg lg--assign"></span> 布置
        <span class="lg lg--due"></span> 截止
        <span class="lg lg--overdue"></span> 已逾期
      </p>
    </div>
  `,
  styles: [`
    .tt-page { padding: 22px 30px 40px; max-width: 1560px; margin: 0 auto; }
    .tt-head { display: flex; align-items: flex-start; justify-content: space-between; gap: 20px; }
    .tt-head h1 { margin: 0; font-size: 21px; font-weight: 650; }
    .tt-sub { margin: 8px 0 0; max-width: 760px; font-size: 13px; line-height: 1.7; color: #64757a; }
    .tt-toolbar { display: flex; align-items: center; gap: 8px; margin: 16px 0 12px; flex-wrap: wrap; }
    .tt-switch { display: inline-flex; border: 1px solid #e0e8e5; border-radius: 8px; overflow: hidden; margin-right: 8px; }
    .tt-switch__btn { padding: 6px 16px; border: 0; background: #fff; font-size: 13px; color: #64757a; cursor: pointer; }
    .tt-switch__btn.on { background: #087f7a; color: #fff; font-weight: 600; }
    .tt-range { margin: 0 0 0 8px; font-size: 15px; font-weight: 600; }
    .tt-stats { margin-left: auto; display: flex; gap: 14px; font-size: 12px; color: #64757a; }
    .tt-stats b { color: #203b40; font-size: 14px; }
    .tt-stats .is-warn, .tt-stats .is-warn b { color: #d63b6a; }
    .tt-loading, .tt-empty { padding: 70px 0; display: grid; place-items: center; }
    .tt-day { border: 1px solid #e0e8e5; border-radius: 12px; background: #fff; overflow: hidden; }
    .tt-day__row { display: flex; min-height: 46px; border-bottom: 1px solid #f0f4f3; }
    .tt-day__row:last-child { border-bottom: 0; }
    .tt-day__hour { flex: 0 0 62px; padding: 6px 8px; font-size: 11px; color: #96a5aa; background: #fafcfb; border-right: 1px solid #f0f4f3; }
    .tt-day__slot { flex: 1 1 auto; padding: 4px 8px; display: flex; flex-wrap: wrap; gap: 6px; }
    .tt-week { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: 8px; }
    .tt-week__col { border: 1px solid #e0e8e5; border-radius: 10px; background: #fff; overflow: hidden; min-height: 220px; }
    .tt-week__col.is-today { border-color: #087f7a; box-shadow: 0 0 0 2px rgba(8,127,122,.12); }
    .tt-week__head { padding: 8px 10px; border-bottom: 1px solid #f0f4f3; background: #fafcfb; display: flex; justify-content: space-between; font-size: 12px; }
    .tt-week__head b { color: #203b40; }
    .tt-week__head span { color: #96a5aa; }
    .tt-week__body { padding: 8px; display: flex; flex-direction: column; gap: 6px; }
    .tt-week__empty { color: #c8d3d0; text-align: center; padding: 10px 0; font-size: 12px; }
    .tt-month { border: 1px solid #e0e8e5; border-radius: 12px; background: #fff; overflow: hidden; }
    .tt-month__weekdays { display: grid; grid-template-columns: repeat(7, 1fr); background: #fafcfb; border-bottom: 1px solid #f0f4f3; }
    .tt-month__weekdays span { padding: 8px; text-align: center; font-size: 12px; color: #64757a; }
    .tt-month__grid { display: grid; grid-template-columns: repeat(7, 1fr); }
    .tt-month__cell { min-height: 96px; padding: 6px; border-right: 1px solid #f0f4f3; border-bottom: 1px solid #f0f4f3; display: flex; flex-direction: column; gap: 3px; }
    .tt-month__cell.is-out { background: #fbfcfc; }
    .tt-month__cell.is-today { background: rgba(8,127,122,.05); }
    .tt-month__date { font-size: 12px; color: #64757a; }
    .tt-month__more { font-size: 11px; color: #96a5aa; }
    .tt-dot { font-size: 11px; padding: 1px 5px; border-radius: 4px; background: rgba(8,127,122,.1); color: #087f7a;
              overflow: hidden; text-overflow: ellipsis; white-space: nowrap; cursor: default; }
    .tt-dot.is-due { background: rgba(214,162,30,.15); color: #8a6a06; }
    .tt-dot.is-overdue { background: rgba(214,59,106,.14); color: #d63b6a; }
    .tt-chip { display: flex; flex-direction: column; gap: 1px; padding: 4px 8px; border-radius: 6px;
               background: rgba(8,127,122,.1); border-left: 3px solid #087f7a; cursor: default; }
    .tt-chip b { font-size: 12px; font-weight: 600; color: #203b40; }
    .tt-chip span { font-size: 11px; color: #64757a; }
    .tt-chip.is-due { background: rgba(214,162,30,.13); border-left-color: #d6a21e; }
    .tt-chip.is-overdue { background: rgba(214,59,106,.12); border-left-color: #d63b6a; }
    .tt-chip--block span { display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .tt-chip__meta { color: #96a5aa !important; }
    .tt-legend { margin: 14px 0 0; font-size: 12px; color: #96a5aa; display: flex; align-items: center; gap: 8px; }
    .lg { width: 10px; height: 10px; border-radius: 3px; display: inline-block; margin-left: 10px; }
    .lg--assign { background: rgba(8,127,122,.5); }
    .lg--due { background: rgba(214,162,30,.6); }
    .lg--overdue { background: rgba(214,59,106,.6); }
  `],
})
export class ClassroomTimetableComponent implements OnInit {
  private readonly api = inject(ClassroomTimetableService);
  private readonly message = inject(NzMessageService);

  readonly views: { key: ViewMode; label: string }[] = [
    { key: 'day', label: '日' },
    { key: 'week', label: '周' },
    { key: 'month', label: '月' },
  ];
  readonly weekNames = ['一', '二', '三', '四', '五', '六', '日'];
  readonly hours = Array.from({ length: 24 }, (_, i) => i);

  readonly view = signal<ViewMode>('week');
  readonly anchor = signal<Date>(startOfDay(new Date()));
  readonly loading = signal(true);
  readonly timetable = signal<Timetable | null>(null);
  readonly courseId = signal<string | null>(null);

  readonly tt = computed(() => this.timetable());

  readonly rangeLabel = computed(() => {
    const v = this.view();
    const d = this.anchor();
    if (v === 'day') {
      return `${d.getFullYear()} 年 ${d.getMonth() + 1} 月 ${d.getDate()} 日`;
    }
    if (v === 'week') {
      const s = startOfWeek(d);
      const e = addDays(s, 6);
      return `${s.getMonth() + 1}-${s.getDate()} ~ ${e.getMonth() + 1}-${e.getDate()}`;
    }
    return `${d.getFullYear()} 年 ${d.getMonth() + 1} 月`;
  });

  readonly weekDays = computed<CalendarCell[]>(() => {
    const s = startOfWeek(this.anchor());
    return Array.from({ length: 7 }, (_, i) => this.cell(addDays(s, i), s));
  });

  readonly monthCells = computed<CalendarCell[]>(() => {
    const a = this.anchor();
    const first = new Date(a.getFullYear(), a.getMonth(), 1);
    const start = startOfWeek(first);
    return Array.from({ length: 42 }, (_, i) => {
      const d = addDays(start, i);
      return this.cell(d, first);
    });
  });

  ngOnInit(): void {
    this.load();
  }

  setView(v: ViewMode): void {
    this.view.set(v);
    this.load();
  }

  onCourseChange(id: string | null): void {
    this.courseId.set(id);
    this.load();
  }

  goToday(): void {
    this.anchor.set(startOfDay(new Date()));
    this.load();
  }

  shift(dir: number): void {
    const d = this.anchor();
    const v = this.view();
    if (v === 'day') {
      this.anchor.set(addDays(d, dir));
    } else if (v === 'week') {
      this.anchor.set(addDays(d, dir * 7));
    } else {
      this.anchor.set(new Date(d.getFullYear(), d.getMonth() + dir, 1));
    }
    this.load();
  }

  private range(): { from: Date; to: Date } {
    const d = this.anchor();
    const v = this.view();
    if (v === 'day') {
      return { from: startOfDay(d), to: endOfDay(d) };
    }
    if (v === 'week') {
      const s = startOfWeek(d);
      return { from: s, to: endOfDay(addDays(s, 6)) };
    }
    const first = new Date(d.getFullYear(), d.getMonth(), 1);
    const last = new Date(d.getFullYear(), d.getMonth() + 1, 0);
    return { from: startOfDay(first), to: endOfDay(last) };
  }

  load(): void {
    const { from, to } = this.range();
    this.loading.set(true);
    this.api.get(toIso(from), toIso(to), this.courseId()).subscribe({
      next: (r) => { this.timetable.set(r); this.loading.set(false); },
      error: () => { this.loading.set(false); this.message.error('课表加载失败'); },
    });
  }

  private cell(date: Date, ref: Date): CalendarCell {
    const items = (this.tt()?.items ?? []).filter((i) => sameDay(new Date(i.at), date));
    return {
      date,
      inCurrent: date.getMonth() === ref.getMonth(),
      isToday: sameDay(date, new Date()),
      items,
    };
  }

  itemsAtHour(h: number) {
    return (this.tt()?.items ?? []).filter((i) => new Date(i.at).getHours() === h);
  }

  weekDayName(d: Date): string {
    return '周' + this.weekNames[(d.getDay() + 6) % 7];
  }

  timeOf(iso: string): string {
    const d = new Date(iso);
    return `${pad(d.getHours())}:${pad(d.getMinutes())}`;
  }

  /** hover 提示：任务名称 / 日期 / 时间段 / 学校 —— 正好对应采购演示项 ③ 的四个字段。 */
  tip(i: TimetableItem): string {
    const d = new Date(i.at);
    const date = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
    const range = i.endAt ? `${this.timeOf(i.at)} - ${this.timeOf(i.endAt)}` : this.timeOf(i.at);
    const kind = i.kind === 'due' ? '截止' : '布置';
    const school = i.schoolName || '—';
    const progress = `学生 ${i.completedCount}/${i.assignmentCount} 已完成`;
    return `${i.title}\n日期：${date}（${kind}）\n时间：${range}\n学校：${school}\n课程：${i.courseName || '—'}\n智能体：${i.agentName || '—'}\n${progress}`;
  }
}

// ── 日期工具 ──────────────────────────────────────────────
function pad(n: number): string { return n < 10 ? '0' + n : '' + n; }
function startOfDay(d: Date): Date { return new Date(d.getFullYear(), d.getMonth(), d.getDate()); }
function endOfDay(d: Date): Date { return new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59); }
function addDays(d: Date, n: number): Date { const r = new Date(d); r.setDate(r.getDate() + n); return r; }
function startOfWeek(d: Date): Date {
  const s = startOfDay(d);
  const off = (s.getDay() + 6) % 7; // 周一为一周开始
  return addDays(s, -off);
}
function sameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
}
function toIso(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`;
}

import { Component, Input, Output, EventEmitter, inject, signal, computed, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { ResourceReviewService, ResourceReviewDto, ResourceRatingSummaryDto, CreateResourceReviewInput, UpdateResourceReviewInput } from './resource-review.service';

@Component({
  selector: 'app-resource-review',
  standalone: true,
  imports: [
    CommonModule, FormsModule, NzSpinModule, NzRateModule,
    NzInputModule, NzButtonModule, NzEmptyModule,
    NzIconModule, NzModalModule
  ],
  templateUrl: './resource-review.component.html',
  styleUrls: ['./resource-review.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ResourceReviewComponent implements OnInit, OnChanges {
  @Input() resourceId = '';
  /**
   * 单次评价模式（学生端）：每人只能评价一次，提交后表单锁定，
   * 不提供编辑/删除入口。资源评分为全部用户评分的平均值。
   */
  @Input() singleEvaluation = false;
  /**
   * 仅展示模式（资源管理后台/教师端）：完全隐藏发表评价表单，
   * 只展示评分汇总与全部评价列表。优先级高于 singleEvaluation。
   */
  @Input() displayOnly = false;
  @Output() reviewChanged = new EventEmitter<void>();

  private readonly reviewService = inject(ResourceReviewService);
  private readonly message = inject(NzMessageService);

  summary = signal<ResourceRatingSummaryDto | null>(null);
  reviews = signal<ResourceReviewDto[]>([]);
  loading = signal(false);
  submitting = signal(false);

  // 表单状态全部使用 signal：OnPush 下普通字段在异步回调中赋值不会触发变更检测。
  myRating = signal(0);
  myContent = signal('');
  editingReviewId = signal<string | null>(null);

  // ── 回复状态（参考资讯评论回复） ──
  /** 正在回复的目标评价；null 表示不在回复流程中 */
  replyTo = signal<ResourceReviewDto | null>(null);
  replyContent = signal('');
  replySubmitting = signal(false);
  replyModalVisible = signal(false);

  /** 评价 id → 评价（回复链查找与孤儿兜底用） */
  readonly reviewMap = computed(() => new Map(this.reviews().map(r => [r.id, r])));

  /** 一级评价：无 parentId，或父评价不在列表中（脏数据兜底为一级展示） */
  readonly rootReviews = computed(() => {
    const map = this.reviewMap();
    return this.reviews().filter(r => !r.parentId || !map.has(r.parentId));
  });

  private loadedForResourceId = '';

  @ViewChild('reviewInput') private reviewInput?: ElementRef<HTMLTextAreaElement>;

  /** 供父组件“写评价”按钮调用：滚动到表单并聚焦输入框 */
  focusForm() {
    const el = this.reviewInput?.nativeElement;
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'center' });
      // 等滚动动画起步后再聚焦，避免浏览器把平滑滚动截断
      setTimeout(() => el.focus({ preventScroll: true }), 350);
    }
  }

  /** 当前用户是否已评价过（单次评价模式下用于锁定表单） */
  hasReviewed(): boolean {
    return this.editingReviewId() !== null;
  }

  ngOnInit() {
    this.reloadIfNeeded();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['resourceId']) {
      this.reloadIfNeeded();
    }
  }

  /** 资源切换（相关资源跳转复用组件）时重置并重载，避免显示旧资源的评论 */
  private reloadIfNeeded() {
    if (!this.resourceId || this.resourceId === this.loadedForResourceId) return;
    this.loadedForResourceId = this.resourceId;
    this.resetForm();
    this.loadSummary();
    this.loadReviews();
  }

  private resetForm() {
    this.myRating.set(0);
    this.myContent.set('');
    this.editingReviewId.set(null);
    this.summary.set(null);
    this.reviews.set([]);
    this.replyTo.set(null);
    this.replyContent.set('');
    this.replyModalVisible.set(false);
  }

  private refresh() {
    this.loadedForResourceId = '';
    this.reloadIfNeeded();
    this.reviewChanged.emit();
  }

  private loadSummary() {
    if (!this.resourceId) return;
    const rid = this.resourceId;
    this.reviewService.getRatingSummary(rid).subscribe({
      next: (data) => {
        // 防止切换资源时的竞态：只接受当前资源的响应
        if (rid !== this.resourceId) return;
        this.summary.set(data);
        if (data.myReview) {
          this.myRating.set(data.myReview.rating);
          this.myContent.set(data.myReview.content || '');
          this.editingReviewId.set(data.myReview.id);
        } else {
          this.myRating.set(0);
          this.myContent.set('');
          this.editingReviewId.set(null);
        }
      }
    });
  }

  private loadReviews() {
    if (!this.resourceId) return;
    const rid = this.resourceId;
    this.loading.set(true);
    this.reviewService.getResourceReviews(rid, 0, 50).subscribe({
      next: (data) => {
        if (rid !== this.resourceId) return;
        this.reviews.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  submitReview() {
    // 单次评价模式：已评价用户直接拦截
    if (this.singleEvaluation && this.hasReviewed()) {
      this.message.warning('您已评价过此资源');
      return;
    }

    const rating = this.myRating();
    if (rating < 1 || rating > 5) {
      this.message.warning('请选择评分（1-5星）');
      return;
    }

    this.submitting.set(true);
    const content = this.myContent()?.trim() || undefined;
    const editingId = this.editingReviewId();

    // 单次评价模式下只走新建；非单次模式且已有评价时走更新
    if (editingId && !this.singleEvaluation) {
      const input: UpdateResourceReviewInput = { rating, content };
      this.reviewService.update(editingId, input).subscribe({
        next: () => {
          this.submitting.set(false);
          this.message.success('评价更新成功');
          this.refresh();
        },
        error: (err) => {
          this.submitting.set(false);
          this.message.error(err?.error?.error?.message || '更新失败');
        }
      });
    } else {
      const input: CreateResourceReviewInput = {
        resourceId: this.resourceId,
        rating,
        content
      };
      this.reviewService.create(input).subscribe({
        next: () => {
          this.submitting.set(false);
          this.message.success('评价提交成功');
          this.refresh();
        },
        error: (err) => {
          this.submitting.set(false);
          // 后端重复评价抛 AlreadyReviewed，给出明确提示
          const msg: string = err?.error?.error?.message || err?.error?.error?.code || '';
          if (msg.includes('AlreadyReviewed')) {
            this.message.warning('您已评价过此资源');
            this.refresh();
          } else {
            this.message.error(err?.error?.error?.message || '提交失败');
          }
        }
      });
    }
  }

  deleteReview(id: string) {
    this.reviewService.delete(id).subscribe({
      next: () => {
        this.message.success('评价已删除');
        this.refresh();
      },
      error: (err) => this.message.error(err?.error?.error?.message || '删除失败')
    });
  }

  // ── 回复（参考资讯评论回复：嵌套展示 + 弹窗发表） ──

  /** 某一级评价下的全部回复（含楼中楼，统一挂根下按时间正序展示） */
  repliesOf(rootId: string): ResourceReviewDto[] {
    return this.repliesByRoot().get(rootId) ?? [];
  }

  /** 评价变化时一次性归组，避免模板对每个根评价重复扫描整棵评价树（O(n²)） */
  private readonly repliesByRoot = computed(() => {
    const map = this.reviewMap();
    const grouped = new Map<string, ResourceReviewDto[]>();
    const rootOf = (r: ResourceReviewDto): string | null => {
      let pid = r.parentId;
      const seen = new Set<string>([r.id]);
      while (pid) {
        if (seen.has(pid)) return null;
        seen.add(pid);
        const parent = map.get(pid);
        if (!parent) return null;
        if (!parent.parentId) return parent.id;
        pid = parent.parentId;
      }
      return null;
    };
    for (const r of this.reviews()) {
      if (!r.parentId) continue;
      const rootId = rootOf(r);
      if (!rootId) continue;
      const list = grouped.get(rootId);
      if (list) list.push(r);
      else grouped.set(rootId, [r]);
    }
    for (const list of grouped.values()) {
      list.sort((a, b) => +new Date(a.creationTime) - +new Date(b.creationTime));
    }
    return grouped;
  });

  /** 打开回复弹窗：目标为被回复的评价（可为一级或楼中回复） */
  openReplyModal(review: ResourceReviewDto) {
    this.replyTo.set(review);
    this.replyContent.set('');
    this.replyModalVisible.set(true);
  }

  closeReplyModal() {
    if (this.replySubmitting()) return;
    this.replyModalVisible.set(false);
    this.replyContent.set('');
    this.replyTo.set(null);
  }

  submitReply() {
    const target = this.replyTo();
    const content = this.replyContent().trim();
    if (!target || !content || this.replySubmitting()) return;
    this.replySubmitting.set(true);
    this.reviewService.create({
      resourceId: this.resourceId,
      parentId: target.id,
      rating: 0,
      content,
    }).subscribe({
      next: () => {
        this.replySubmitting.set(false);
        this.replyModalVisible.set(false);
        this.replyContent.set('');
        this.replyTo.set(null);
        this.message.success('回复已发布');
        this.refresh();
      },
      error: (err) => {
        this.replySubmitting.set(false);
        this.message.error(err?.error?.error?.message || '回复提交失败');
      }
    });
  }
}

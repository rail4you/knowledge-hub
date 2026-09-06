import { Component, Input, Output, EventEmitter, inject, signal, OnInit, OnChanges, SimpleChanges, ChangeDetectionStrategy, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { ResourceReviewService, ResourceReviewDto, ResourceRatingSummaryDto, CreateResourceReviewInput, UpdateResourceReviewInput } from './resource-review.service';

@Component({
  selector: 'app-resource-review',
  standalone: true,
  imports: [
    CommonModule, FormsModule, NzSpinModule, NzRateModule,
    NzInputModule, NzButtonModule, NzListModule, NzAvatarModule, NzEmptyModule,
    NzDividerModule, NzIconModule
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

  getRatingPercent(star: number): number {
    const s = this.summary();
    if (!s || s.totalReviews === 0) return 0;
    return Math.round((s.ratingDistribution[star - 1] / s.totalReviews) * 100);
  }
}

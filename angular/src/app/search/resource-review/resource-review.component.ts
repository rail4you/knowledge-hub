import { Component, Input, inject, signal, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzRateModule } from 'ng-zorro-antd/rate';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { NzMessageService } from 'ng-zorro-antd/message';
import { ResourceReviewService, ResourceReviewDto, ResourceRatingSummaryDto, CreateResourceReviewInput, UpdateResourceReviewInput } from './resource-review.service';

@Component({
  selector: 'app-resource-review',
  standalone: true,
  imports: [
    CommonModule, FormsModule, NzCardModule, NzSpinModule, NzRateModule,
    NzInputModule, NzButtonModule, NzListModule, NzAvatarModule, NzTagModule, NzEmptyModule,
    NzDividerModule
  ],
  templateUrl: './resource-review.component.html',
  styleUrls: ['./resource-review.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ResourceReviewComponent implements OnInit {
  @Input() resourceId = '';

  private readonly reviewService = inject(ResourceReviewService);
  private readonly message = inject(NzMessageService);

  summary = signal<ResourceRatingSummaryDto | null>(null);
  reviews = signal<ResourceReviewDto[]>([]);
  loading = signal(false);
  submitting = signal(false);

  myRating = 0;
  myContent = '';
  editingReviewId: string | null = null;

  ngOnInit() {
    this.loadSummary();
    this.loadReviews();
  }

  private loadSummary() {
    if (!this.resourceId) return;
    this.reviewService.getRatingSummary(this.resourceId).subscribe({
      next: (data) => {
        this.summary.set(data);
        if (data.myReview) {
          this.myRating = data.myReview.rating;
          this.myContent = data.myReview.content || '';
          this.editingReviewId = data.myReview.id;
        }
      }
    });
  }

  private loadReviews() {
    if (!this.resourceId) return;
    this.loading.set(true);
    this.reviewService.getResourceReviews(this.resourceId, 0, 50).subscribe({
      next: (data) => {
        this.reviews.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  submitReview() {
    if (this.myRating < 1 || this.myRating > 5) {
      this.message.warning('请选择评分（1-5星）');
      return;
    }

    this.submitting.set(true);

    if (this.editingReviewId) {
      const input: UpdateResourceReviewInput = {
        rating: this.myRating,
        content: this.myContent || undefined
      };
      this.reviewService.update(this.editingReviewId, input).subscribe({
        next: () => {
          this.submitting.set(false);
          this.message.success('评价更新成功');
          this.loadSummary();
          this.loadReviews();
        },
        error: () => {
          this.submitting.set(false);
          this.message.error('更新失败');
        }
      });
    } else {
      const input: CreateResourceReviewInput = {
        resourceId: this.resourceId,
        rating: this.myRating,
        content: this.myContent || undefined
      };
      this.reviewService.create(input).subscribe({
        next: () => {
          this.submitting.set(false);
          this.message.success('评价提交成功');
          this.loadSummary();
          this.loadReviews();
        },
        error: () => {
          this.submitting.set(false);
          this.message.error('提交失败');
        }
      });
    }
  }

  deleteReview(id: string) {
    this.reviewService.delete(id).subscribe({
      next: () => {
        this.message.success('评价已删除');
        this.editingReviewId = null;
        this.myRating = 0;
        this.myContent = '';
        this.loadSummary();
        this.loadReviews();
      },
      error: () => this.message.error('删除失败')
    });
  }

  getRatingPercent(star: number): number {
    const s = this.summary();
    if (!s || s.totalReviews === 0) return 0;
    return Math.round((s.ratingDistribution[star - 1] / s.totalReviews) * 100);
  }

  getTrendHeight(star: number): number {
    const s = this.summary();
    if (!s || s.totalReviews === 0) return 0;
    const percent = (s.ratingDistribution[star - 1] / s.totalReviews) * 100;
    return Math.max(2, Math.round(percent * 0.08));
  }
}

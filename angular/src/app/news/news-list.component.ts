import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzPaginationModule } from 'ng-zorro-antd/pagination';
import { NewsArticleDto, NewsCategoryDto, NewsService } from './news.service';

@Component({
  selector: 'app-news-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzIconModule,
    NzSelectModule,
    NzTagModule,
    NzSpinModule,
    NzEmptyModule,
    NzPaginationModule,
  ],
  templateUrl: './news-list.component.html',
  styleUrls: ['./news-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsListComponent implements OnInit {
  private readonly newsService = inject(NewsService);

  readonly loading = signal(false);
  readonly articles = signal<NewsArticleDto[]>([]);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  readonly categories = signal<NewsCategoryDto[]>([]);
  readonly filter = signal('');
  readonly categoryId = signal<string | null>(null);
  // 文章列表分页（服务端分页）
  readonly total = signal(0);
  readonly pageIndex = signal(1);
  readonly pageSize = signal(10);

  ngOnInit(): void {
    this.loadCategories();
    this.loadArticles();
    this.loadHotArticles();
  }

  loadCategories(): void {
    this.newsService.getCategoryTree().subscribe({
      next: categories => this.categories.set(categories),
    });
  }

  loadArticles(): void {
    this.loading.set(true);
    this.newsService.getPublishedArticles({
      filter: this.filter() || undefined,
      categoryId: this.categoryId() || undefined,
      skipCount: (this.pageIndex() - 1) * this.pageSize(),
      maxResultCount: this.pageSize(),
    }).subscribe({
      next: result => {
        this.articles.set(result.items || []);
        this.total.set(result.totalCount || 0);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  onPageIndexChange(index: number): void {
    this.pageIndex.set(index);
    this.loadArticles();
  }

  onFilterChange(): void {
    this.pageIndex.set(1);
    this.loadArticles();
  }

  loadHotArticles(): void {
    this.newsService.getHotArticles().subscribe({
      next: items => this.hotArticles.set(items || []),
    });
  }

  categoryOptions(): NewsCategoryDto[] {
    const result: NewsCategoryDto[] = [];
    const append = (items: NewsCategoryDto[]) => {
      for (const item of items) {
        result.push(item);
        if (item.children?.length) {
          append(item.children);
        }
      }
    };

    append(this.categories());
    return result;
  }
}

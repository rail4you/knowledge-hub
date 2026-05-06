import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NewsArticleDto, NewsCategoryDto, NewsService } from './news.service';

@Component({
  selector: 'app-news-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    NzCardModule,
    NzButtonModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzSpinModule,
    NzEmptyModule,
  ],
  templateUrl: './news-list.component.html',
  styleUrls: ['./news-list.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NewsListComponent implements OnInit {
  private readonly newsService = inject(NewsService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly articles = signal<NewsArticleDto[]>([]);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  readonly categories = signal<NewsCategoryDto[]>([]);
  readonly filter = signal('');
  readonly categoryId = signal<string | null>(null);

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
      skipCount: 0,
      maxResultCount: 30,
    }).subscribe({
      next: result => {
        this.articles.set(result.items || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
      },
    });
  }

  loadHotArticles(): void {
    this.newsService.getHotArticles().subscribe({
      next: items => this.hotArticles.set(items || []),
    });
  }

  openArticle(id: string): void {
    this.router.navigate(['/news', id]);
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

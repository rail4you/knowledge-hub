import { ChangeDetectionStrategy, Component, OnInit, OnDestroy, inject, signal, computed } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NewsArticleDto, NewsCategoryDto, NewsService } from '../../news/news.service';

interface HeroSlide {
  title: string;
  subtitle: string;
  description: string;
  tag: string;
  icon: string;
  gradient: string;
}

interface StatItem {
  label: string;
  value: number;
  suffix: string;
  icon: string;
  color: string;
}

interface CategoryChip {
  id: string | null;
  name: string;
  icon: string;
  color: string;
}

@Component({
  selector: 'app-student-news',
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    DecimalPipe,
    FormsModule,
    NzIconModule,
    NzSpinModule,
  ],
  templateUrl: './student-news.component.html',
  styleUrls: ['./student-news.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentNewsComponent implements OnInit {
  private readonly newsService = inject(NewsService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly loading = signal(false);
  readonly articles = signal<NewsArticleDto[]>([]);
  readonly hotArticles = signal<NewsArticleDto[]>([]);
  readonly categories = signal<NewsCategoryDto[]>([]);
  readonly filter = signal('');
  readonly categoryId = signal<string | null>(null);
  readonly activeHeroSlide = signal(0);
  private heroTimer: ReturnType<typeof setInterval> | null = null;

  // 数据统计（从实际数据计算）
  readonly stats = computed<StatItem[]>(() => {
    const all = this.articles();
    const hot = this.hotArticles();
    const totalViews = all.reduce((sum, a) => sum + (a.viewCount || 0), 0);
    const viewsText = totalViews >= 10000
      ? (totalViews / 10000).toFixed(1) + '万'
      : totalViews.toString();
    return [
      { label: '资讯总数', value: all.length, suffix: '篇', icon: 'file-text', color: '#1e6ce8' },
      { label: '热门资讯', value: hot.length, suffix: '篇', icon: 'fire', color: '#f59e0b' },
      { label: '总阅读量', value: totalViews, suffix: totalViews >= 10000 ? '万次' : '次', icon: 'eye', color: '#10b981' },
    ];
  });

  readonly heroSlides = signal<HeroSlide[]>([
    {
      title: '资讯中心',
      subtitle: 'NEWS CENTER',
      description: '汇集行业动态、政策解读、教学资讯与企业新闻，让你随时掌握最新前沿信息。',
      tag: '资讯简介',
      icon: 'bulb',
      gradient: 'linear-gradient(135deg, #1e6ce8 0%, #00b7ff 100%)',
    },
    {
      title: '行业动态',
      subtitle: 'INDUSTRY NEWS',
      description: '紧跟职业教育发展最新动态，解读国家政策、院校改革、产业升级等热门话题。',
      tag: '行业动态',
      icon: 'rise',
      gradient: 'linear-gradient(135deg, #0c4cb8 0%, #1e6ce8 60%, #00b7ff 100%)',
    },
    {
      title: '教学资讯',
      subtitle: 'TEACHING HIGHLIGHTS',
      description: '分享优秀教学案例、课程建设经验与教师成长故事，启发教学创新灵感。',
      tag: '教学资讯',
      icon: 'read',
      gradient: 'linear-gradient(135deg, #2563eb 0%, #3b82f6 60%, #60a5fa 100%)',
    },
  ]);

  // 分类筛选 chips
  readonly categoryChips = signal<CategoryChip[]>([
    { id: null, name: '全部资讯', icon: 'appstore', color: '#1e6ce8' },
  ]);

  readonly featuredArticles = computed(() => {
    return this.articles()
      .filter(a => a.isTop)
      .slice(0, 2);
  });

  readonly latestArticles = computed(() => {
    const featuredIds = new Set(this.featuredArticles().map(a => a.id));
    return this.articles()
      .filter(a => !featuredIds.has(a.id))
      .slice(0, 12);
  });

  readonly trendingArticles = computed(() => {
    return [...this.articles()]
      .sort((a, b) => (b.viewCount || 0) - (a.viewCount || 0))
      .slice(0, 5);
  });

  ngOnInit(): void {
    this.loadCategories();
    this.loadArticles();
    this.loadHotArticles();
    this.startHeroAutoPlay();
  }

  ngOnDestroy(): void {
    this.stopHeroAutoPlay();
  }

  startHeroAutoPlay() {
    this.stopHeroAutoPlay();
    this.heroTimer = setInterval(() => {
      this.activeHeroSlide.set((this.activeHeroSlide() + 1) % this.heroSlides().length);
    }, 6000);
  }

  stopHeroAutoPlay() {
    if (this.heroTimer) {
      clearInterval(this.heroTimer);
      this.heroTimer = null;
    }
  }

  selectHeroSlide(index: number) {
    this.activeHeroSlide.set(index);
    this.startHeroAutoPlay();
  }

  loadCategories(): void {
    this.newsService.getCategoryTree().subscribe({
      next: categories => {
        this.categories.set(categories || []);
        // 同步生成快捷分类 chips
        const chips: CategoryChip[] = [
          { id: null, name: '全部资讯', icon: 'appstore', color: '#1e6ce8' },
        ];
        const colorPalette = ['#7c3aed', '#10b981', '#f59e0b', '#ef4444', '#06b6d4', '#ec4899'];
        let colorIndex = 0;
        const flat = this.categoryOptions();
        flat.slice(0, 6).forEach(cat => {
          chips.push({
            id: cat.id,
            name: cat.name,
            icon: 'folder',
            color: colorPalette[colorIndex % colorPalette.length],
          });
          colorIndex++;
        });
        this.categoryChips.set(chips);
      },
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
        this.message.error('资讯加载失败');
      },
    });
  }

  loadHotArticles(): void {
    this.newsService.getHotArticles().subscribe({
      next: items => this.hotArticles.set(items || []),
    });
  }

  openArticle(id: string): void {
    this.router.navigate(['/student/news', id]);
  }

  selectCategory(id: string | null): void {
    this.categoryId.set(id);
    this.loadArticles();
  }

  onSearch(): void {
    this.loadArticles();
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

  /**
   * 文章封面渐变（基于标题 hash，确保稳定）
   */
  coverGradient(article: NewsArticleDto): string {
    return this.gradientByKey(article.title || article.id || 'x', article.categoryName || '');
  }

  /**
   * 基于分类名生成稳定的渐变色（用于侧边栏分类图标）
   */
  gradientByCategory(categoryName: string): string {
    return this.gradientByKey(categoryName, categoryName);
  }

  private gradientByKey(primary: string, secondary: string): string {
    const palettes = [
      'linear-gradient(135deg, #1e6ce8 0%, #00b7ff 100%)',
      'linear-gradient(135deg, #7c3aed 0%, #ec4899 100%)',
      'linear-gradient(135deg, #10b981 0%, #06b6d4 100%)',
      'linear-gradient(135deg, #f59e0b 0%, #ef4444 100%)',
      'linear-gradient(135deg, #ec4899 0%, #f97316 100%)',
      'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)',
      'linear-gradient(135deg, #06b6d4 0%, #0ea5e9 100%)',
      'linear-gradient(135deg, #f43f5e 0%, #fb7185 100%)',
    ];
    const key = (primary || 'x') + (secondary || '');
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = (hash * 31 + key.charCodeAt(i)) | 0;
    }
    return palettes[Math.abs(hash) % palettes.length];
  }

  hasCover(article: NewsArticleDto): boolean {
    return !!article.coverImageUrl && article.coverImageUrl.trim().length > 0;
  }

  parseTags(tags?: string): string[] {
    if (!tags) return [];
    return tags.split(/[,，;；\s]+/).map(t => t.trim()).filter(t => t.length > 0).slice(0, 3);
  }

  /** 热门资讯排行榜数字 */
  rankNumber(index: number): string {
    return (index + 1).toString().padStart(2, '0');
  }

  /** 封面图加载失败时隐藏 img，露出底层渐变 pattern */
  onCoverError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (img) {
      img.classList.add('is-hidden');
    }
  }
}

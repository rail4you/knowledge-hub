import { Component, ElementRef, inject, signal, OnInit, AfterViewInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { SearchService } from '../search/search.service';
import { ResourceReviewComponent } from '../search/resource-review/resource-review.component';

interface HitMatch {
  id: number;
  sentence: string;
  element: HTMLElement;
}

@Component({
  selector: 'app-document-viewer',
  standalone: true,
  imports: [
    CommonModule,
    NzSpinModule,
    NzButtonModule,
    NzIconModule,
    NzBadgeModule,
    NzTagModule,
    ResourceReviewComponent
  ],
  template: `
    <div class="document-viewer">
      <div class="toolbar">
        <button nz-button (click)="prevPage()" [disabled]="currentPage() <= 1">
          <span nz-icon nzType="left"></span>
          上一页
        </button>
        <span class="page-info">{{ currentPage() }} / {{ totalPages() }}</span>
        <button nz-button (click)="nextPage()" [disabled]="currentPage() >= totalPages()">
          下一页
          <span nz-icon nzType="right"></span>
        </button>
        <button nz-button (click)="goBack()">
          <span nz-icon nzType="rollback"></span>
          返回
        </button>
        <span class="toolbar-spacer"></span>
        <button nz-button [nzType]="showReviewPanel() ? 'primary' : 'default'" (click)="toggleReviewPanel()">
          <span nz-icon nzType="star" nzTheme="outline"></span>
          评价
        </button>
      </div>

      <div class="body-area">
        @if (hits().length > 0) {
          <div class="hit-nav">
            <div class="hit-nav-header">
              <nz-badge [nzCount]="hits().length" nzOverflowCount="9999">
                <span class="hit-nav-title">匹配高亮</span>
              </nz-badge>
            </div>
            <div class="hit-nav-list">
              @for (hit of hits(); track hit.id) {
                <div
                  class="hit-nav-item"
                  [class.active]="activeHit() === hit.id"
                  (click)="scrollToHit(hit.id)"
                >
                  <nz-tag [nzColor]="'blue'" class="hit-index">{{ hit.id }}</nz-tag>
                  <span class="hit-sentence">{{ hit.sentence }}</span>
                </div>
              }
            </div>
          </div>
        }

        <div class="page-container" #pageContainer>
          @if (loading()) {
            <nz-spin nzSimple nzTip="加载中..."></nz-spin>
          } @else {
            <div class="text-content" [innerHTML]="pageContent()"></div>
          }
        </div>

        @if (showReviewPanel()) {
          <div class="review-panel">
            <div class="review-panel-header">
              <span class="review-panel-title">资源评价</span>
              <button nz-button nzSize="small" nzType="text" (click)="toggleReviewPanel()">
                <span nz-icon nzType="close"></span>
              </button>
            </div>
            <div class="review-panel-content">
              @if (resourceId()) {
                <app-resource-review [resourceId]="resourceId()"></app-resource-review>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .document-viewer {
      display: flex;
      flex-direction: column;
      height: 100vh;
      background: #f5f5f5;
    }

    .toolbar {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 12px 24px;
      background: white;
      border-bottom: 1px solid #e8e8e8;
      box-shadow: 0 2px 8px rgba(0,0,0,0.06);
    }

    .page-info {
      font-size: 14px;
      color: #666;
    }

    .toolbar-spacer {
      flex: 1;
    }

    .body-area {
      flex: 1;
      display: flex;
      min-height: 0;
    }

    .review-panel {
      width: 380px;
      min-width: 380px;
      background: white;
      border-left: 1px solid #e8e8e8;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .review-panel-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 12px 16px;
      border-bottom: 1px solid #e8e8e8;
      background: #fafafa;
    }

    .review-panel-title {
      font-size: 14px;
      font-weight: 500;
      color: #333;
    }

    .review-panel-content {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
    }

    .hit-nav {
      width: 280px;
      min-width: 280px;
      background: white;
      border-right: 1px solid #e8e8e8;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }

    .hit-nav-header {
      padding: 12px 16px;
      border-bottom: 1px solid #e8e8e8;
      background: #fafafa;
    }

    .hit-nav-title {
      font-size: 14px;
      font-weight: 500;
      color: #333;
    }

    .hit-nav-list {
      flex: 1;
      overflow-y: auto;
      padding: 8px;
    }

    .hit-nav-item {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      padding: 8px 10px;
      border-radius: 6px;
      cursor: pointer;
      transition: background-color 0.2s, box-shadow 0.2s;
      margin-bottom: 4px;
    }

    .hit-nav-item:hover {
      background: #e6f7ff;
    }

    .hit-nav-item.active {
      background: #bae7ff;
      box-shadow: inset 3px 0 0 #1890ff;
    }

    .hit-index {
      flex-shrink: 0;
      line-height: 20px;
    }

    .hit-sentence {
      font-size: 13px;
      color: #555;
      line-height: 1.5;
      word-break: break-all;
    }

    .hit-nav-item.active .hit-sentence {
      color: #000;
    }

    .page-container {
      flex: 1;
      min-width: 0;
      min-height: 0;
      overflow-y: auto;
      overflow-x: hidden;
      padding: 24px;
    }

    .text-content {
      background: white;
      max-width: 800px;
      width: 100%;
      margin: 0 auto;
      padding: 40px;
      border-radius: 4px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      line-height: 1.8;
      font-size: 14px;
      white-space: pre-wrap;
      word-break: break-word;
      overflow-wrap: anywhere;
      overflow-x: hidden;
      box-sizing: border-box;
    }

    .text-content ::ng-deep * {
      max-width: 100%;
      word-break: break-word;
      overflow-wrap: anywhere;
      box-sizing: border-box;
    }

    .text-content ::ng-deep pre,
    .text-content ::ng-deep code {
      white-space: pre-wrap;
    }

    .text-content ::ng-deep mark {
      background-color: #fff3cd;
      padding: 2px 4px;
      border-radius: 2px;
      transition: background-color 0.3s, box-shadow 0.3s;
    }

    .text-content ::ng-deep mark.hit-active {
      background-color: #ffc53d;
      box-shadow: 0 0 0 3px rgba(250, 173, 20, 0.4);
    }
  `]
})
export class DocumentViewerComponent implements OnInit, AfterViewInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  @ViewChild('pageContainer', { static: true }) pageContainerRef!: ElementRef<HTMLDivElement>;

  currentPage = signal(1);
  totalPages = signal(1);
  pageContent = signal('');
  loading = signal(false);
  resourceName = signal('');
  resourceId = signal('');

  hits = signal<HitMatch[]>([]);
  activeHit = signal<number>(-1);
  showReviewPanel = signal(false);

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    const page = this.route.snapshot.queryParamMap.get('page');
    const state = history.state;

    if (id) {
      this.resourceId.set(id);
    }
    if (page) {
      this.currentPage.set(parseInt(page, 10));
    }
    if (state.content) {
      this.pageContent.set(state.content);
      this.loading.set(false);
    } else {
      this.loadPageContent();
    }
  }

  ngAfterViewInit() {
    // Content is set synchronously in ngOnInit, so marks are available in AfterViewInit
    this.extractHits();
  }

  private extractHits() {
    const container = this.pageContainerRef?.nativeElement;
    if (!container) return;

    const marks = container.querySelectorAll('.text-content mark');
    if (marks.length === 0) return;

    const hitMatches: HitMatch[] = [];
    marks.forEach((mark, index) => {
      const htmlEl = mark as HTMLElement;
      htmlEl.setAttribute('data-hit-index', String(index + 1));
      hitMatches.push({
        id: index + 1,
        sentence: this.extractSentence(htmlEl),
        element: htmlEl,
      });
    });

    this.hits.set(hitMatches);
  }

  private extractSentence(mark: Element): string {
    const parent = mark.parentElement;
    if (!parent) return mark.textContent || '';

    const text = parent.textContent || '';
    const markText = mark.textContent || '';
    const index = text.indexOf(markText);

    if (index === -1) return markText;

    const start = Math.max(0, index - 30);
    const end = Math.min(text.length, index + markText.length + 30);
    let sentence = text.substring(start, end);

    if (start > 0) sentence = '...' + sentence;
    if (end < text.length) sentence = sentence + '...';

    return sentence.trim();
  }

  scrollToHit(id: number) {
    const container = this.pageContainerRef?.nativeElement;
    if (!container) return;

    // Remove previous active highlight
    if (this.activeHit() > 0) {
      const prevMark = container.querySelector(`mark[data-hit-index="${this.activeHit()}"]`);
      prevMark?.classList.remove('hit-active');
    }

    this.activeHit.set(id);

    const mark = container.querySelector(`mark[data-hit-index="${id}"]`) as HTMLElement | null;
    if (!mark) return;

    mark.classList.add('hit-active');
    mark.scrollIntoView({ behavior: 'smooth', block: 'center' });

    // Scroll nav item into view
    const navItem = document.querySelector(`.hit-nav-item:nth-child(${id})`);
    navItem?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  }

  loadPageContent() {
    this.loading.set(true);
    const content = history.state.content;
    if (content) {
      this.pageContent.set(content);
      this.loading.set(false);
    } else {
      this.pageContent.set('<p>无内容</p>');
      this.loading.set(false);
    }
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update(v => v - 1);
    }
  }

  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(v => v + 1);
    }
  }

  goBack() {
    this.router.navigate(['/search'], {
      state: { searchState: history.state.searchState }
    });
  }

  toggleReviewPanel() {
    this.showReviewPanel.update(v => !v);
  }
}

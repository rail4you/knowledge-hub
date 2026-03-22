import { Component, inject, signal, OnInit, viewChild, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzMessageService } from 'ng-zorro-antd/message';
import { SearchService } from '../search/search.service';

@Component({
  selector: 'app-document-viewer',
  standalone: true,
  imports: [
    CommonModule,
    NzSpinModule,
    NzButtonModule,
    NzIconModule
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
      </div>

      <div class="page-container" #pageContainer>
        @if (loading()) {
          <nz-spin nzSimple nzTip="加载中..."></nz-spin>
        } @else {
          <div class="text-content" [innerHTML]="pageContent()"></div>
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
    
    .page-container {
      flex: 1;
      overflow-y: auto;
      padding: 24px;
      display: flex;
      justify-content: center;
    }
    
    .text-content {
      background: white;
      max-width: 800px;
      width: 100%;
      padding: 40px;
      border-radius: 4px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      line-height: 1.8;
      font-size: 14px;
      white-space: pre-wrap;
    }
    
    .text-content ::ng-deep mark {
      background-color: #fff3cd;
      padding: 2px 4px;
      border-radius: 2px;
    }
  `]
})
export class DocumentViewerComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  resourceId = input.required<string>();
  page = input<number>();
  highlight = input<string>();

  currentPage = signal(1);
  totalPages = signal(1);
  pageContent = signal('');
  loading = signal(false);
  resourceName = signal('');

  ngOnInit() {
    if (this.page()) {
      this.currentPage.set(this.page());
    }
    this.loadPageContent();
  }

  loadPageContent() {
    this.loading.set(true);
    // TODO: Call API to get page content with highlighted text
    // For now, just show placeholder
    setTimeout(() => {
      this.pageContent.set('<p>文档内容加载中...</p><p>当前页码: ' + this.currentPage() + '</p>');
      this.loading.set(false);
    }, 500);
  }

  prevPage() {
    if (this.currentPage() > 1) {
      this.currentPage.update(v => v - 1);
      this.loadPageContent();
    }
  }

  nextPage() {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(v => v + 1);
      this.loadPageContent();
    }
  }

  goBack() {
    this.router.navigate(['/search']);
  }
}

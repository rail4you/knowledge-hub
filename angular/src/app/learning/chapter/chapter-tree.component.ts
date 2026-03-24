import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzButtonModule } from 'ng-zorro-antd/button';

@Component({
  selector: 'app-chapter-tree',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzIconModule,
    NzButtonModule
  ],
  template: `
    <div class="chapter-tree-container">
      <nz-card nzTitle="课程章节">
        <div class="placeholder">
          <span nz-icon nzType="folder" nzTheme="outline" class="icon"></span>
          <p>章节管理功能开发中...</p>
          <p class="hint">课程章节支持嵌套结构，可添加、编辑、删除章节</p>
        </div>
      </nz-card>
    </div>
  `,
  styles: [`
    .chapter-tree-container {
      margin-top: 16px;
    }
    .placeholder {
      text-align: center;
      padding: 40px 20px;
      color: #999;
    }
    .icon {
      font-size: 48px;
      color: #d9d9d9;
    }
    .hint {
      font-size: 12px;
      color: #ccc;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterTreeComponent {
  @Input() courseId!: string;
}
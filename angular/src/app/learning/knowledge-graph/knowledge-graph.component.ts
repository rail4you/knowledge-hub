import { Component, signal, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzTabsModule } from 'ng-zorro-antd/tabs';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzMessageService } from 'ng-zorro-antd/message';
import { CourseService } from '../../proxy/courses/course.service';
import { ChapterTreeGraphComponent } from './chapter-tree-graph.component';
import { MindMapGraphComponent } from './mind-map-graph.component';
import { KnowledgeNetworkGraphComponent } from './knowledge-network-graph.component';

interface KnowledgeResourceDto {
  id: string;
  name: string;
  description?: string;
  importanceLevel?: string;
  difficulty?: number;
  sortOrder?: number;
}

interface ChapterDto {
  id: string;
  courseId?: string;
  parentId?: string;
  title?: string;
  description?: string;
  sortOrder?: number;
  knowledgeResources?: KnowledgeResourceDto[];
  children?: ChapterDto[];
}

interface CourseDetailDto {
  id?: string;
  title?: string;
  description?: string;
  chapters?: ChapterDto[];
}

@Component({
  selector: 'app-knowledge-graph',
  standalone: true,
  imports: [
    CommonModule,
    NzCardModule,
    NzTabsModule,
    NzButtonModule,
    NzIconModule,
    NzSpinModule,
    ChapterTreeGraphComponent,
    MindMapGraphComponent,
    KnowledgeNetworkGraphComponent
  ],
  template: `
    <nz-card [nzTitle]="cardTitle" [nzExtra]="extraTemplate">
      <ng-template #cardTitle>
        <span nz-icon nzType="apartment" nzTheme="outline"></span>
        知识图谱 - {{ courseDetail()?.title || '' }}
      </ng-template>
      <ng-template #extraTemplate>
        <button nz-button nzType="default" (click)="goBack()">
          <span nz-icon nzType="arrow-left"></span>
          返回课程
        </button>
      </ng-template>
      
      <nz-spin [nzSpinning]="loading()">
        @if (courseDetail()) {
          <nz-tabset>
            <nz-tab nzTitle="章节树状图">
              <app-chapter-tree-graph [chapters]="courseDetail()?.chapters || []" />
            </nz-tab>
            <nz-tab nzTitle="思维导图">
              <app-mind-map-graph [chapters]="courseDetail()?.chapters || []" />
            </nz-tab>
            <nz-tab nzTitle="知识网络">
              <app-knowledge-network-graph [chapters]="courseDetail()?.chapters || []" />
            </nz-tab>
          </nz-tabset>
        } @else if (!loading()) {
          <div class="empty-state">
            <span nz-icon nzType="folder-open" nzTheme="outline" class="empty-icon"></span>
            <p>暂无章节数据</p>
          </div>
        }
      </nz-spin>
    </nz-card>
  `,
  styles: [`
    .empty-state {
      text-align: center;
      padding: 60px 20px;
      color: #999;
    }
    .empty-icon {
      font-size: 64px;
      color: #d9d9d9;
    }
    :host ::ng-deep .ant-card-body {
      padding: 0;
    }
    :host ::ng-deep .ant-tabs-nav {
      padding: 16px 24px 0;
      margin-bottom: 0;
    }
    :host ::ng-deep .ant-tabs-content {
      padding: 24px;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class KnowledgeGraphComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly courseService = inject(CourseService);
  private readonly message = inject(NzMessageService);
  
  courseDetail = signal<any>(null);
  loading = signal(true);
  courseId = signal('');
  
  ngOnInit() {
    const courseId = this.route.snapshot.paramMap.get('courseId');
    if (courseId) {
      this.courseId.set(courseId);
      this.loadCourseDetail(courseId);
    } else {
      this.message.error('课程ID不存在');
      this.loading.set(false);
    }
  }
  
  loadCourseDetail(courseId: string) {
    this.loading.set(true);
    this.courseService.getDetail(courseId).subscribe({
      next: (res) => {
        this.courseDetail.set(res);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load course detail:', err);
        this.message.error('加载课程详情失败');
        this.loading.set(false);
      }
    });
  }
  
  goBack() {
    this.router.navigate(['/learning/course-detail', this.courseId()]);
  }
}

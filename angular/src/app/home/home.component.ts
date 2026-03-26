import { Component, inject, OnInit } from '@angular/core';
import { AuthService, ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { RouterLink, Router, ActivatedRoute } from '@angular/router';
import { OAuthService } from 'angular-oauth2-oidc';
import { filter, take } from 'rxjs/operators';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  imports: [LocalizationPipe, RouterLink]
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthService);
  private configService = inject(ConfigStateService);
  private oauthService = inject(OAuthService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated
  }

  ngOnInit() {
    if (this.hasLoggedIn) {
      const returnUrl = this.route.snapshot.queryParams['returnUrl'];
      if (!returnUrl) {
        this.router.navigate(['/']);
      }
    }
  }

  login() {
    this.authService.navigateToLogin();
  }

  features = [
    {
      title: '资源管理',
      description: '上传、管理教学资源，支持多种文档格式，自动建立索引',
      icon: 'folder-open',
      route: '/resources',
      color: '#3b82f6'
    },
    {
      title: '智能搜索',
      description: '基于 AI 的全文搜索，快速定位所需知识内容',
      icon: 'search',
      route: '/search',
      color: '#10b981'
    },
    {
      title: 'AI 助手',
      description: '智能对话问答，辅助学习与知识探索',
      icon: 'comment-dots',
      route: '/ai/chat',
      color: '#8b5cf6'
    },
    {
      title: '教案生成',
      description: 'AI 辅助生成专业教案，提升备课效率',
      icon: 'lightbulb',
      route: '/ai/lesson-plan',
      color: '#f59e0b'
    },
    {
      title: '课程学习',
      description: '系统化课程管理，跟踪学习进度',
      icon: 'book',
      route: '/learning/my-courses',
      color: '#ec4899'
    },
    {
      title: '知识图谱',
      description: '可视化知识结构，构建完整知识体系',
      icon: 'project-diagram',
      route: '/learning/course-list',
      color: '#06b6d4'
    },
    {
      title: '案例分析',
      description: '深度分析典型案例，提升实践能力',
      icon: 'briefcase',
      route: '/ai/case-analysis',
      color: '#ef4444'
    },
    {
      title: '职业指导',
      description: '个性化职业规划建议，助力职业发展',
      icon: 'graduation-cap',
      route: '/ai/career-guidance',
      color: '#6366f1'
    }
  ];
}

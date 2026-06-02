import { Component, inject, OnInit, signal } from '@angular/core';
import { AuthService, ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { RouterLink, Router, ActivatedRoute } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { CommonModule } from '@angular/common';
import { hasRole } from '../auth/current-user.utils';
import { PortalService } from '../proxy/portal/portal.service';
import { TenantResourceSummaryDto } from '../proxy/portal/models';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  imports: [LocalizationPipe, RouterLink, NzSpinModule, NzEmptyModule, CommonModule]
})
export class HomeComponent implements OnInit {
  private authService = inject(AuthService);
  private configService = inject(ConfigStateService);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private portalService = inject(PortalService);

  // Tenant list signals
  tenantList = signal<TenantResourceSummaryDto[]>([]);
  loadingTenants = signal(false);

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated
  }

  get isStudent(): boolean {
    return hasRole(this.configService, 'Student');
  }

  ngOnInit() {
    this.loadTenants();
    
    if (this.hasLoggedIn) {
      const returnUrl = this.route.snapshot.queryParams['returnUrl'];
      if (!returnUrl && this.isStudent) {
        // 学生登录后跳转到 Angular 学生端资源库
        this.router.navigate(['/student']);
      }
    }
  }

  /**
   * 加载租户列表
   */
  loadTenants() {
    this.loadingTenants.set(true);
    this.portalService.getPublicTenantList().subscribe({
      next: (tenants) => {
        this.tenantList.set(tenants || []);
        this.loadingTenants.set(false);
      },
      error: (err) => {
        console.error('Failed to load tenants:', err);
        this.tenantList.set([]);
        this.loadingTenants.set(false);
      }
    });
  }

  /**
   * 点击租户卡片
   */
  onTenantClick(tenant: TenantResourceSummaryDto) {
    // 未登录用户点击租户可以查看预览
    // 如果已登录，可以跳转到该租户的学生端
    if (this.hasLoggedIn && this.isStudent) {
      this.router.navigate(['/student'], { queryParams: { tenantId: tenant.id } });
    } else {
      // 未登录用户，显示提示或跳转到登录
      this.login();
    }
  }

  login() {
    this.authService.navigateToLogin();
  }

  logout() {
    this.authService.logout().subscribe(() => {
      // 使用完整页面刷新清除 ABP 框架缓存的布局状态
      window.location.href = '/';
    });
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
import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { AuthService, ConfigStateService } from '@abp/ng.core';
import { hasRole } from '../auth/current-user.utils';
import { PortalService, TenantResourceSummaryDto } from '../proxy/portal/portal.service';

@Component({
  selector: 'app-portal-home',
  standalone: true,
  imports: [CommonModule, RouterLink, NzSpinModule],
  template: `
    <div class="portal-home">
      <!-- 顶部导航 -->
      <header class="portal-header">
        <div class="header-container">
          <div class="header-left">
            <div class="logo">
              <svg viewBox="0 0 40 40" fill="none">
                <rect width="40" height="40" rx="8" fill="#0056D2"/>
                <path d="M12 12h16v4H12v-4zm0 6h16v4H12v-4zm0 6h12v4H12v-4z" fill="white"/>
              </svg>
            </div>
            <h1 class="site-title">易课通资源库</h1>
          </div>
          
          <div class="header-right">
            @if (hasLoggedIn) {
              <span class="user-name">{{ currentUserName() }}</span>
              <button class="btn-logout" (click)="logout()">退出</button>
            } @else {
              <button class="btn-login" (click)="login()">登录 / 注册</button>
            }
          </div>
        </div>
      </header>

      <!-- 主内容区 -->
      <main class="portal-main">
        <!-- 资源库分类 -->
        <section class="resource-section">
          <div class="section-header">
            <h2 class="section-title">资源库</h2>
            <div class="filter-tabs">
              <span class="filter-tab active">全部</span>
              <span class="filter-tab">国家级</span>
              <span class="filter-tab">省级</span>
              <span class="filter-tab">校级</span>
            </div>
          </div>
          
          @if (loading()) {
            <div class="loading">
              <nz-spin nzSimple></nz-spin>
              <span>加载中...</span>
            </div>
          } @else if (tenantList().length > 0) {
            <div class="resource-grid">
              @for (tenant of tenantList(); track tenant.id) {
                <div class="resource-card" (click)="onTenantClick(tenant)">
                  <div class="card-header">
                    <div class="card-icon">
                      <svg viewBox="0 0 40 40" fill="none">
                        <rect width="40" height="40" rx="8" fill="#0056D2"/>
                        <path d="M12 12h16v4H12v-4zm0 6h16v4H12v-4zm0 6h12v4H12v-4z" fill="white"/>
                      </svg>
                    </div>
                    <div class="card-info">
                      <h3 class="card-title">{{ tenant.name }}</h3>
                      <span class="card-badge">{{ tenant.industryField || '教育机构' }}</span>
                    </div>
                  </div>
                  <p class="card-desc">{{ tenant.description || '教育资源库' }}</p>
                  <div class="card-stats">
                    <span><i class="icon-book"></i>{{ tenant.courseCount }} 课程</span>
                    <span><i class="icon-file"></i>{{ tenant.resourceCount }} 资源</span>
                    <span><i class="icon-layer"></i>{{ tenant.microMajorCount }} 微专业</span>
                  </div>
                  <div class="card-action">
                    <span>预览资源库</span>
                    <i class="icon-arrow">›</i>
                  </div>
                </div>
              }
            </div>
          } @else {
            <div class="empty-state">
              <p>暂无资源库</p>
            </div>
          }
        </section>

        <!-- 好课推荐 -->
        <section class="course-section">
          <div class="section-header">
            <h2 class="section-title">好课推荐</h2>
            <a class="more-link">更多 ›</a>
          </div>
          <div class="course-grid">
            <div class="course-card">
              <div class="course-thumb"></div>
              <div class="course-info">
                <h4 class="course-name">课程名称</h4>
                <p class="course-school">学校名称</p>
                <p class="course-teacher">教师名称</p>
              </div>
            </div>
            <div class="course-card">
              <div class="course-thumb"></div>
              <div class="course-info">
                <h4 class="course-name">课程名称</h4>
                <p class="course-school">学校名称</p>
                <p class="course-teacher">教师名称</p>
              </div>
            </div>
            <div class="course-card">
              <div class="course-thumb"></div>
              <div class="course-info">
                <h4 class="course-name">课程名称</h4>
                <p class="course-school">学校名称</p>
                <p class="course-teacher">教师名称</p>
              </div>
            </div>
          </div>
        </section>

        <!-- 专业课程分类 -->
        <section class="category-section">
          <div class="section-header">
            <h2 class="section-title">专业课程</h2>
            <div class="category-tabs">
              <span class="category-tab active">财经商贸大类</span>
              <span class="category-tab">电子与信息大类</span>
              <span class="category-tab">装备制造大类</span>
              <span class="category-tab">教育与体育大类</span>
            </div>
          </div>
        </section>
      </main>

      <!-- 底部 -->
      <footer class="portal-footer">
        <div class="footer-container">
          <p>© 2026 易课通资源库系统 - 让学习更高效</p>
        </div>
      </footer>
    </div>
  `,
  styles: [`
    :host {
      display: block;
    }
    
    .portal-home {
      min-height: 100vh;
      background: #f5f7fa;
    }
    
    /* 顶部导航 */
    .portal-header {
      background: white;
      box-shadow: 0 2px 8px rgba(0,0,0,0.08);
      position: sticky;
      top: 0;
      z-index: 100;
    }
    
    .header-container {
      max-width: 1200px;
      margin: 0 auto;
      padding: 0 24px;
      height: 64px;
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    
    .header-left {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    
    .logo svg {
      width: 36px;
      height: 36px;
    }
    
    .site-title {
      font-size: 1.25rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0;
    }
    
    .header-right {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    
    .user-name {
      color: #666;
    }
    
    .btn-login, .btn-logout {
      padding: 8px 20px;
      border-radius: 6px;
      font-size: 14px;
      cursor: pointer;
      border: none;
      transition: all 0.2s;
    }
    
    .btn-login {
      background: #0056D2;
      color: white;
    }
    
    .btn-login:hover {
      background: #004499;
    }
    
    .btn-logout {
      background: transparent;
      border: 1px solid #ddd;
      color: #666;
    }
    
    .btn-logout:hover {
      background: #f5f5f5;
    }
    
    /* 主内容区 */
    .portal-main {
      max-width: 1200px;
      margin: 0 auto;
      padding: 32px 24px;
    }
    
    /* 资源库区域 */
    .resource-section {
      margin-bottom: 48px;
    }
    
    .section-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 24px;
    }
    
    .section-title {
      font-size: 1.5rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0;
    }
    
    .filter-tabs, .category-tabs {
      display: flex;
      gap: 8px;
    }
    
    .filter-tab, .category-tab {
      padding: 6px 16px;
      border-radius: 20px;
      font-size: 14px;
      color: #666;
      cursor: pointer;
      background: white;
      border: 1px solid #e5e7eb;
      transition: all 0.2s;
    }
    
    .filter-tab:hover, .category-tab:hover {
      border-color: #0056D2;
      color: #0056D2;
    }
    
    .filter-tab.active, .category-tab.active {
      background: #0056D2;
      color: white;
      border-color: #0056D2;
    }
    
    .more-link {
      color: #0056D2;
      font-size: 14px;
      text-decoration: none;
    }
    
    .loading, .empty-state {
      text-align: center;
      padding: 48px;
      color: #999;
    }
    
    .resource-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
      gap: 20px;
    }
    
    .resource-card {
      background: white;
      border-radius: 12px;
      padding: 20px;
      cursor: pointer;
      transition: all 0.3s;
      border: 1px solid #e5e7eb;
    }
    
    .resource-card:hover {
      transform: translateY(-4px);
      box-shadow: 0 8px 24px rgba(0,0,0,0.1);
      border-color: #0056D2;
    }
    
    .card-header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 12px;
    }
    
    .card-icon svg {
      width: 48px;
      height: 48px;
    }
    
    .card-info {
      flex: 1;
    }
    
    .card-title {
      font-size: 1rem;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 4px 0;
    }
    
    .card-badge {
      font-size: 12px;
      color: #0056D2;
      background: rgba(0,86,210,0.1);
      padding: 2px 8px;
      border-radius: 4px;
    }
    
    .card-desc {
      font-size: 14px;
      color: #666;
      margin-bottom: 16px;
      line-height: 1.5;
    }
    
    .card-stats {
      display: flex;
      gap: 16px;
      padding: 12px 0;
      border-top: 1px solid #f0f0f0;
      border-bottom: 1px solid #f0f0f0;
      margin-bottom: 12px;
    }
    
    .card-stats span {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 13px;
      color: #666;
    }
    
    .card-stats .icon-book::before { content: '📚'; }
    .card-stats .icon-file::before { content: '📄'; }
    .card-stats .icon-layer::before { content: '📁'; }
    
    .card-action {
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 14px;
      color: #0056D2;
      font-weight: 500;
    }
    
    .card-action .icon-arrow {
      font-size: 18px;
      transition: transform 0.2s;
    }
    
    .resource-card:hover .card-action .icon-arrow {
      transform: translateX(4px);
    }
    
    /* 课程区域 */
    .course-section {
      margin-bottom: 48px;
    }
    
    .course-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 20px;
    }
    
    .course-card {
      background: white;
      border-radius: 12px;
      overflow: hidden;
      border: 1px solid #e5e7eb;
    }
    
    .course-thumb {
      height: 140px;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }
    
    .course-info {
      padding: 16px;
    }
    
    .course-name {
      font-size: 14px;
      font-weight: 600;
      color: #1a1a1a;
      margin: 0 0 4px 0;
    }
    
    .course-school, .course-teacher {
      font-size: 12px;
      color: #999;
      margin: 0;
    }
    
    /* 分类区域 */
    .category-section {
      margin-bottom: 48px;
    }
    
    /* 底部 */
    .portal-footer {
      background: white;
      padding: 24px;
      text-align: center;
      border-top: 1px solid #e5e7eb;
    }
    
    .footer-container p {
      margin: 0;
      color: #999;
      font-size: 14px;
    }
    
    @media (max-width: 768px) {
      .header-container {
        padding: 0 16px;
      }
      
      .portal-main {
        padding: 24px 16px;
      }
      
      .resource-grid {
        grid-template-columns: 1fr;
      }
      
      .course-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class PortalHomeComponent implements OnInit {
  private authService = inject(AuthService);
  private configService = inject(ConfigStateService);
  private router = inject(Router);
  private portalService = inject(PortalService);

  tenantList = signal<TenantResourceSummaryDto[]>([]);
  loading = signal(false);
  currentUserName = signal('用户');

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  get isStudent(): boolean {
    return hasRole(this.configService, 'Student');
  }

  ngOnInit() {
    this.loadCurrentUser();
    this.loadTenants();
    
    if (this.hasLoggedIn && this.isStudent) {
      // 学生已登录，跳转到学生端
      this.router.navigate(['/student']);
    }
  }

  loadCurrentUser() {
    const currentUser = this.configService.getDeep('currentUser') as Record<string, unknown> | undefined;
    const userName = currentUser?.['userName'];
    if (typeof userName === 'string' && userName.trim()) {
      this.currentUserName.set(userName);
    }
  }

  loadTenants() {
    this.loading.set(true);
    this.portalService.getPublicTenantList().subscribe({
      next: (tenants) => {
        this.tenantList.set(tenants || []);
        this.loading.set(false);
      },
      error: (err) => {
        console.error('Failed to load tenants:', err);
        this.tenantList.set([]);
        this.loading.set(false);
      }
    });
  }

  onTenantClick(tenant: TenantResourceSummaryDto) {
    if (this.hasLoggedIn && this.isStudent) {
      this.router.navigate(['/student'], { queryParams: { tenantId: tenant.id } });
    } else {
      this.login();
    }
  }

  login() {
    this.authService.navigateToLogin();
  }

  logout() {
    this.authService.logout().subscribe(() => {
      window.location.href = '/';
    });
  }
}
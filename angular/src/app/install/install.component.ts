import { Component, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzStepsModule } from 'ng-zorro-antd/steps';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzRadioModule } from 'ng-zorro-antd/radio';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzMessageService } from 'ng-zorro-antd/message';
import { Router } from '@angular/router';
import { InstallService } from './install.service';
import { InstallInputDto } from './models';

@Component({
  selector: 'app-install',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    NzStepsModule,
    NzFormModule,
    NzInputModule,
    NzButtonModule,
    NzRadioModule,
    NzCardModule,
    NzAlertModule,
  ],
  template: `
    <div class="install-container">
      <nz-card class="install-card">
        <h1 class="install-title">知识中心平台安装向导</h1>
        
        <nz-steps [nzCurrent]="currentStep" nzSize="small" class="install-steps">
          <nz-step nzTitle="许可证验证"></nz-step>
          <nz-step nzTitle="选择版本"></nz-step>
          <nz-step nzTitle="创建管理员"></nz-step>
        </nz-steps>

        <!-- Step 0: License Validation -->
        <div *ngIf="currentStep === 0" class="step-content">
          <h2>步骤 1: 许可证验证</h2>
          <p class="step-desc">请输入您的许可证密钥以继续安装</p>
          
          <nz-form-item>
            <nz-form-label [nzSpan]="6" nzRequired>许可证密钥</nz-form-label>
            <nz-form-control [nzSpan]="14">
              <input nz-input [(ngModel)]="installData.licenseKey" 
                     placeholder="例如: KH-STANDARD-2024-FREE" />
            </nz-form-control>
          </nz-form-item>
          
          <div *ngIf="licenseError" class="license-error">
            <nz-alert nzType="error" [nzMessage]="licenseError" nzShowIcon></nz-alert>
          </div>
          
          <div class="step-actions">
            <button nz-button nzType="primary" (click)="validateLicense()">
              验证许可证
            </button>
          </div>
        </div>

        <!-- Step 1: Edition Selection -->
        <div *ngIf="currentStep === 1" class="step-content">
          <h2>步骤 2: 选择版本</h2>
          <p class="step-desc">请选择适合您学校的版本类型</p>
          
          <div class="edition-options">
            <nz-radio-group [(ngModel)]="installData.edition">
              <label nz-radio nzValue="Basic" class="edition-option">
                <nz-card nzHoverable [nzTitle]="'基础版'">
                  <p>单租户模式</p>
                  <p>不支持联盟</p>
                  <p>单级审批流程</p>
                  <p class="edition-desc">适合涉密或校内专属使用场景</p>
                </nz-card>
              </label>
              
              <label nz-radio nzValue="Standard" class="edition-option">
                <nz-card nzHoverable [nzTitle]="'标准版'">
                  <p>多租户模式</p>
                  <p>支持跨校联盟</p>
                  <p>两级审批流程</p>
                  <p class="edition-desc">适合多校协作场景</p>
                </nz-card>
              </label>
            </nz-radio-group>
          </div>
          
          <div *ngIf="installData.edition === 'Standard'" class="standard-note">
            <nz-alert nzType="info" 
                      nzMessage="标准版需要有效的许可证密钥" 
                      nzShowIcon></nz-alert>
          </div>
          
          <div class="step-actions">
            <button nz-button (click)="prevStep()">上一步</button>
            <button nz-button nzType="primary" (click)="nextStep()">下一步</button>
          </div>
        </div>

        <!-- Step 2: Admin Creation -->
        <div *ngIf="currentStep === 2" class="step-content">
          <h2>步骤 3: 创建管理员</h2>
          <p class="step-desc">请设置超级管理员账户信息</p>
          
          <nz-form-item>
            <nz-form-label [nzSpan]="6" nzRequired>用户名</nz-form-label>
            <nz-form-control [nzSpan]="14">
              <input nz-input [(ngModel)]="installData.adminUsername" placeholder="admin" />
            </nz-form-control>
          </nz-form-item>
          
          <nz-form-item>
            <nz-form-label [nzSpan]="6" nzRequired>邮箱</nz-form-label>
            <nz-form-control [nzSpan]="14">
              <input nz-input type="email" [(ngModel)]="installData.adminEmail" 
                     placeholder="admin@example.com" />
            </nz-form-control>
          </nz-form-item>
          
          <nz-form-item>
            <nz-form-label [nzSpan]="6" nzRequired>密码</nz-form-label>
            <nz-form-control [nzSpan]="14">
              <input nz-input type="password" [(ngModel)]="installData.adminPassword" 
                     placeholder="请输入密码" />
            </nz-form-control>
          </nz-form-item>
          
          <nz-form-item>
            <nz-form-label [nzSpan]="6" nzRequired>确认密码</nz-form-label>
            <nz-form-control [nzSpan]="14">
              <input nz-input type="password" [(ngModel)]="confirmPassword" 
                     placeholder="请确认密码" />
            </nz-form-control>
          </nz-form-item>
          
          <div *ngIf="installError" class="install-error">
            <nz-alert nzType="error" [nzMessage]="installError" nzShowIcon></nz-alert>
          </div>
          
          <div class="step-actions">
            <button nz-button (click)="prevStep()">上一步</button>
            <button nz-button nzType="primary" (click)="install()" 
                    [nzLoading]="isInstalling">
              完成安装
            </button>
          </div>
        </div>

        <!-- Success -->
        <div *ngIf="currentStep === 3" class="step-content success-content">
          <nz-result nzStatus="success" nzTitle="安装成功！">
            <div nz-result-extra>
              <p>系统已安装完成，正在跳转登录页面...</p>
            </div>
          </nz-result>
        </div>
      </nz-card>
    </div>
  `,
  styles: [`
    .install-container {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      padding: 20px;
    }
    
    .install-card {
      max-width: 700px;
      width: 100%;
    }
    
    .install-title {
      text-align: center;
      margin-bottom: 30px;
      color: #1890ff;
    }
    
    .install-steps {
      margin-bottom: 40px;
    }
    
    .step-content {
      min-height: 300px;
    }
    
    .step-content h2 {
      margin-bottom: 10px;
    }
    
    .step-desc {
      color: #666;
      margin-bottom: 30px;
    }
    
    .edition-options {
      margin-bottom: 20px;
    }
    
    .edition-option {
      display: block;
      margin-bottom: 20px;
    }
    
    .edition-option nz-card {
      width: 100%;
    }
    
    .edition-desc {
      color: #999;
      font-size: 12px;
      margin-top: 10px;
    }
    
    .standard-note {
      margin-bottom: 20px;
    }
    
    .step-actions {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      margin-top: 30px;
    }
    
    .license-error,
    .install-error {
      margin-bottom: 20px;
    }
    
    .success-content {
      display: flex;
      justify-content: center;
      align-items: center;
    }
    
    nz-form-item {
      margin-bottom: 20px;
    }
  `]
})
export class InstallComponent {
  currentStep = 0;
  installData: InstallInputDto = {
    licenseKey: '',
    edition: 'Basic',
    adminUsername: 'admin',
    adminPassword: '',
    adminEmail: 'admin@knowledgehub.com'
  };
  confirmPassword = '';
  licenseError = '';
  installError = '';
  isInstalling = false;

  constructor(
    private installService: InstallService,
    private message: NzMessageService,
    private router: Router
  ) {}

  validateLicense(): void {
    this.licenseError = '';
    
    if (!this.installData.licenseKey) {
      this.licenseError = '请输入许可证密钥';
      return;
    }
    
    if (this.installData.edition === 'Standard' && 
        !this.installData.licenseKey.startsWith('KH-STANDARD-')) {
      this.licenseError = '标准版需要有效的许可证密钥（以 KH-STANDARD- 开头）';
      return;
    }
    
    this.nextStep();
  }

  nextStep(): void {
    this.currentStep++;
  }

  prevStep(): void {
    this.currentStep--;
  }

  async install(): Promise<void> {
    this.installError = '';
    
    if (!this.installData.adminPassword) {
      this.installError = '请输入密码';
      return;
    }
    
    if (this.installData.adminPassword !== this.confirmPassword) {
      this.installError = '两次输入的密码不一致';
      return;
    }
    
    if (this.installData.adminPassword.length < 6) {
      this.installError = '密码长度至少为6位';
      return;
    }
    
    this.isInstalling = true;
    
    try {
      await this.installService.install(this.installData).toPromise();
      this.currentStep = 3;
      
      setTimeout(() => {
        window.location.href = '/account/login';
      }, 2000);
    } catch (error) {
      this.installError = '安装失败，请重试';
      this.isInstalling = false;
    }
  }
}
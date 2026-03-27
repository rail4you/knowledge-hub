import { Component, ChangeDetectionStrategy, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { EditionService, EditionDto } from './edition.service';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-edition-info',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzAlertModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
  ],
  template: `
    <nz-card [nzTitle]="'版本信息'" [nzBordered]="false">
      <div class="edition-info">
        <div class="info-row">
          <span class="label">当前版本：</span>
          <span class="value" [class.basic]="edition?.edition === 'Basic'" [class.standard]="edition?.edition === 'Standard'">
            {{ edition?.edition === 'Basic' ? '基础版' : '标准版' }}
          </span>
        </div>
        
        <div class="info-row">
          <span class="label">最大租户数：</span>
          <span class="value">{{ edition?.maxTenantCount === -1 ? '无限制' : edition?.maxTenantCount }}</span>
        </div>
        
        <div class="info-row">
          <span class="label">联盟管理：</span>
          <span class="value">{{ edition?.isAllianceEnabled ? '已启用' : '未启用' }}</span>
        </div>
        
        <div class="info-row">
          <span class="label">两级审批：</span>
          <span class="value">{{ edition?.isTwoLevelApprovalEnabled ? '已启用' : '未启用' }}</span>
        </div>
        
        <div *ngIf="edition?.edition === 'Basic'" class="upgrade-section">
          <nz-alert nzType="info" nzMessage="升级到标准版以解锁更多功能" nzShowIcon></nz-alert>
          <button nz-button nzType="primary" (click)="showUpgradeModal()" class="upgrade-btn">
            升级到标准版
          </button>
        </div>
      </div>
    </nz-card>

    <nz-modal 
      [(nzVisible)]="isUpgradeModalVisible" 
      nzTitle="升级到标准版" 
      (nzOnCancel)="isUpgradeModalVisible = false"
      (nzOnOk)="upgrade()"
      [nzOkLoading]="isUpgrading"
      [nzOkText]="isUpgrading ? '升级中...' : '确认升级'">
      
      <ng-container *nzModalContent>
        <nz-alert nzType="warning" 
                  nzMessage="升级后无法降级到基础版" 
                  nzShowIcon
                  class="warning-alert"></nz-alert>
        
        <nz-form-item class="license-input">
          <nz-form-label [nzSpan]="6" nzRequired>许可证密钥</nz-form-label>
          <nz-form-control [nzSpan]="16">
            <input nz-input 
                   [(ngModel)]="licenseKey" 
                   placeholder="请输入标准版许可证密钥"
                   [disabled]="isUpgrading" />
          </nz-form-control>
        </nz-form-item>
        
        <div class="license-hint">
          许可证格式：KH-STANDARD-*
        </div>
        
        <div *ngIf="upgradeError" class="error-message">
          <nz-alert nzType="error" [nzMessage]="upgradeError" nzShowIcon></nz-alert>
        </div>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .edition-info {
      padding: 10px 0;
    }
    
    .info-row {
      display: flex;
      align-items: center;
      margin-bottom: 16px;
      font-size: 16px;
    }
    
    .label {
      color: #666;
      width: 120px;
    }
    
    .value {
      font-weight: 500;
    }
    
    .value.basic {
      color: #1890ff;
    }
    
    .value.standard {
      color: #52c41a;
    }
    
    .upgrade-section {
      margin-top: 24px;
      padding-top: 24px;
      border-top: 1px solid #f0f0f0;
    }
    
    .upgrade-btn {
      margin-top: 16px;
    }
    
    .warning-alert {
      margin-bottom: 16px;
    }
    
    .license-input {
      margin-top: 16px;
    }
    
    .license-hint {
      font-size: 12px;
      color: #999;
      margin-top: 8px;
    }
    
    .error-message {
      margin-top: 16px;
    }
  `]
})
export class EditionInfoComponent implements OnInit {
  edition: EditionDto | null = null;
  isUpgradeModalVisible = false;
  isUpgrading = false;
  licenseKey = '';
  upgradeError = '';
  
  private editionService = inject(EditionService);
  private http = inject(HttpClient);
  private message = inject(NzMessageService);

  ngOnInit(): void {
    this.loadEdition();
  }

  loadEdition(): void {
    this.editionService.getEdition().subscribe(edition => {
      this.edition = edition;
    });
  }

  showUpgradeModal(): void {
    this.licenseKey = '';
    this.upgradeError = '';
    this.isUpgradeModalVisible = true;
  }

  upgrade(): void {
    this.upgradeError = '';
    
    if (!this.licenseKey) {
      this.upgradeError = '请输入许可证密钥';
      return;
    }
    
    this.isUpgrading = true;
    
    this.http.post('/api/app/edition/upgrade-to-standard', { licenseKey: this.licenseKey })
      .subscribe({
        next: () => {
          this.message.success('升级成功！');
          this.isUpgradeModalVisible = false;
          this.isUpgrading = false;
          this.editionService.clearCache();
          this.loadEdition();
        },
        error: (err) => {
          this.upgradeError = err.error?.error?.message || '升级失败，请检查许可证密钥';
          this.isUpgrading = false;
        }
      });
  }
}
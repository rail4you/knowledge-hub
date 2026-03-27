import { Component, ChangeDetectionStrategy, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzTagModule } from 'ng-zorro-antd/tag';
import { NzAlertModule } from 'ng-zorro-antd/alert';
import { NzBadgeModule } from 'ng-zorro-antd/badge';
import { AllianceService } from '../../proxy/application/alliance/alliance.service';
import { AllianceDto, AllianceMemberDto, PendingAllianceAuditDto, AllianceAuditInputDto } from '../../proxy/application/contracts/alliance/models';
import { AllianceMemberRole, allianceMemberRoleOptions } from '../../proxy/alliance/enums/alliance-member-role.enum';

@Component({
  selector: 'app-alliance-management',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    NzCardModule,
    NzButtonModule,
    NzTableModule,
    NzModalModule,
    NzFormModule,
    NzInputModule,
    NzSelectModule,
    NzTagModule,
    NzAlertModule,
    NzBadgeModule,
  ],
  template: `
    <nz-card nzTitle="联盟管理" [nzBordered]="false">
      <div class="table-operations">
        <button nz-button nzType="primary" (click)="showAllianceModal()">
          创建联盟
        </button>
      </div>
      
      <nz-table
        [nzData]="alliances"
        [nzLoading]="loading"
        [nzPageSize]="10"
        [nzShowSizeChanger]="false">
        <thead>
          <tr>
            <th>联盟名称</th>
            <th>描述</th>
            <th>状态</th>
            <th>成员数</th>
            <th>创建时间</th>
            <th>操作</th>
          </tr>
        </thead>
        <tbody>
          <tr *ngFor="let alliance of alliances">
            <td>{{ alliance.name }}</td>
            <td>{{ alliance.description }}</td>
            <td>
              <nz-tag [nzColor]="alliance.status === 0 ? 'green' : 'red'">
                {{ alliance.status === 0 ? '正常' : '已停用' }}
              </nz-tag>
            </td>
            <td>{{ alliance.memberCount }}</td>
            <td>{{ alliance.creationTime | date: 'yyyy-MM-dd HH:mm' }}</td>
            <td>
              <button nz-button nzType="link" (click)="showMembersModal(alliance)">成员</button>
              <button nz-button nzType="link" nzDanger (click)="deleteAlliance(alliance)">删除</button>
            </td>
          </tr>
        </tbody>
      </nz-table>
    </nz-card>

    <!-- Members Modal -->
    <nz-modal
      [(nzVisible)]="isMembersModalVisible"
      nzTitle="联盟成员"
      [nzWidth]="700"
      (nzOnCancel)="isMembersModalVisible = false"
      [nzFooter]="null">
      <ng-container *nzModalContent>
        <div class="member-header">
          <button nz-button nzType="primary" (click)="showAddMemberModal()">
            添加成员
          </button>
        </div>
        
        <nz-table
          [nzData]="members"
          [nzLoading]="membersLoading"
          [nzPageSize]="10"
          [nzShowSizeChanger]="false"
          style="margin-top: 16px;">
          <thead>
            <tr>
              <th>学校名称</th>
              <th>角色</th>
              <th>加入时间</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let member of members">
              <td>{{ member.tenantName }}</td>
              <td>
                <nz-select
                  [(ngModel)]="member.role"
                  [nzAllowClear]="false"
                  style="width: 120px;"
                  (ngModelChange)="updateMemberRole(member)">
                  <nz-option *ngFor="let opt of allianceMemberRoleOptions" 
                             [nzValue]="opt.value" 
                             [nzLabel]="opt.label">
                  </nz-option>
                </nz-select>
              </td>
              <td>{{ member.joinedTime | date: 'yyyy-MM-dd HH:mm' }}</td>
              <td>
                <button nz-button nzType="link" nzDanger (click)="removeMember(member)">移除</button>
              </td>
            </tr>
          </tbody>
        </nz-table>
      </ng-container>
    </nz-modal>

    <!-- Add Alliance Modal -->
    <nz-modal
      [(nzVisible)]="isAllianceModalVisible"
      [nzTitle]="editingAlliance ? '编辑联盟' : '创建联盟'"
      (nzOnCancel)="isAllianceModalVisible = false"
      (nzOnOk)="saveAlliance()"
      [nzOkLoading]="saving">
      <ng-container *nzModalContent>
        <nz-form-item>
          <nz-form-label [nzSpan]="6" nzRequired>联盟名称</nz-form-label>
          <nz-form-control [nzSpan]="16">
            <input nz-input [(ngModel)]="allianceForm.name" placeholder="请输入联盟名称" />
          </nz-form-control>
        </nz-form-item>
        <nz-form-item>
          <nz-form-label [nzSpan]="6">描述</nz-form-label>
          <nz-form-control [nzSpan]="16">
            <textarea nz-input [(ngModel)]="allianceForm.description" placeholder="请输入描述" rows="3"></textarea>
          </nz-form-control>
        </nz-form-item>
      </ng-container>
    </nz-modal>

    <!-- Add Member Modal -->
    <nz-modal
      [(nzVisible)]="isAddMemberModalVisible"
      nzTitle="添加成员"
      (nzOnCancel)="isAddMemberModalVisible = false"
      (nzOnOk)="addMember()"
      [nzOkLoading]="addingMember">
      <ng-container *nzModalContent>
        <nz-form-item>
          <nz-form-label [nzSpan]="6" nzRequired>租户ID</nz-form-label>
          <nz-form-control [nzSpan]="16">
            <input nz-input [(ngModel)]="memberForm.tenantId" placeholder="请输入租户ID" />
          </nz-form-control>
        </nz-form-item>
        <nz-form-item>
          <nz-form-label [nzSpan]="6" nzRequired>学校名称</nz-form-label>
          <nz-form-control [nzSpan]="16">
            <input nz-input [(ngModel)]="memberForm.tenantName" placeholder="请输入学校名称" />
          </nz-form-control>
        </nz-form-item>
        <nz-form-item>
          <nz-form-label [nzSpan]="6" nzRequired>角色</nz-form-label>
          <nz-form-control [nzSpan]="16">
            <nz-select [(ngModel)]="memberForm.role" style="width: 100%;">
              <nz-option *ngFor="let opt of allianceMemberRoleOptions" 
                         [nzValue]="opt.value" 
                         [nzLabel]="opt.label">
              </nz-option>
            </nz-select>
          </nz-form-control>
        </nz-form-item>
      </ng-container>
    </nz-modal>
  `,
  styles: [`
    .table-operations {
      margin-bottom: 16px;
    }
    
    .member-header {
      margin-bottom: 16px;
    }
  `]
})
export class AllianceManagementComponent implements OnInit {
  private allianceService = inject(AllianceService);
  private message = inject(NzMessageService);

  alliances: AllianceDto[] = [];
  members: AllianceMemberDto[] = [];
  loading = false;
  membersLoading = false;
  saving = false;
  addingMember = false;

  isAllianceModalVisible = false;
  isMembersModalVisible = false;
  isAddMemberModalVisible = false;
  editingAlliance: AllianceDto | null = null;
  currentAllianceId: string | null = null;

  allianceForm = {
    name: '',
    description: ''
  };

  memberForm = {
    tenantId: '',
    tenantName: '',
    role: AllianceMemberRole.Member
  };

  allianceMemberRoleOptions = allianceMemberRoleOptions;

  ngOnInit(): void {
    this.loadAlliances();
  }

  loadAlliances(): void {
    this.loading = true;
    this.allianceService.getList({ maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: (result) => {
        this.alliances = result.items || [];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.message.error('加载联盟列表失败');
      }
    });
  }

  loadMembers(allianceId: string): void {
    this.membersLoading = true;
    this.allianceService.getMembers({ allianceId, maxResultCount: 100, skipCount: 0 } as any).subscribe({
      next: (result) => {
        this.members = result.items || [];
        this.membersLoading = false;
      },
      error: () => {
        this.membersLoading = false;
        this.message.error('加载成员列表失败');
      }
    });
  }

  showAllianceModal(alliance?: AllianceDto): void {
    this.editingAlliance = alliance || null;
    if (alliance) {
      this.allianceForm = {
        name: alliance.name || '',
        description: alliance.description || ''
      };
    } else {
      this.allianceForm = { name: '', description: '' };
    }
    this.isAllianceModalVisible = true;
  }

  saveAlliance(): void {
    if (!this.allianceForm.name) {
      this.message.warning('请输入联盟名称');
      return;
    }

    this.saving = true;
    const input: any = { name: this.allianceForm.name, description: this.allianceForm.description || null };

    if (this.editingAlliance) {
      this.allianceService.update(this.editingAlliance.id!, input).subscribe({
        next: () => {
          this.message.success('更新成功');
          this.isAllianceModalVisible = false;
          this.saving = false;
          this.loadAlliances();
        },
        error: (err: any) => {
          this.message.error(err.error?.error?.message || '更新失败');
          this.saving = false;
        }
      });
    } else {
      this.allianceService.create(input).subscribe({
        next: () => {
          this.message.success('创建成功');
          this.isAllianceModalVisible = false;
          this.saving = false;
          this.loadAlliances();
        },
        error: (err: any) => {
          this.message.error(err.error?.error?.message || '创建失败');
          this.saving = false;
        }
      });
    }
  }

  deleteAlliance(alliance: AllianceDto): void {
    this.allianceService.delete(alliance.id!).subscribe({
      next: () => {
        this.message.success('删除成功');
        this.loadAlliances();
      },
      error: (err: any) => {
        this.message.error(err.error?.error?.message || '删除失败');
      }
    });
  }

  showMembersModal(alliance: AllianceDto): void {
    this.currentAllianceId = alliance.id!;
    this.isMembersModalVisible = true;
    this.loadMembers(alliance.id!);
  }

  showAddMemberModal(): void {
    this.memberForm = { tenantId: '', tenantName: '', role: AllianceMemberRole.Member };
    this.isAddMemberModalVisible = true;
  }

  addMember(): void {
    if (!this.memberForm.tenantId || !this.memberForm.tenantName) {
      this.message.warning('请填写完整信息');
      return;
    }

    this.addingMember = true;
    const input: any = {
      allianceId: this.currentAllianceId,
      tenantId: this.memberForm.tenantId,
      tenantName: this.memberForm.tenantName,
      role: this.memberForm.role
    };

    this.allianceService.addMember(input).subscribe({
      next: () => {
        this.message.success('添加成功');
        this.isAddMemberModalVisible = false;
        this.addingMember = false;
        this.loadMembers(this.currentAllianceId!);
      },
      error: (err: any) => {
        this.message.error(err.error?.error?.message || '添加失败');
        this.addingMember = false;
      }
    });
  }

  removeMember(member: AllianceMemberDto): void {
    this.allianceService.removeMember(member.id!).subscribe({
      next: () => {
        this.message.success('移除成功');
        this.loadMembers(this.currentAllianceId!);
      },
      error: (err: any) => {
        this.message.error(err.error?.error?.message || '移除失败');
      }
    });
  }

  updateMemberRole(member: AllianceMemberDto): void {
    this.allianceService.updateMemberRole(member.id!, member.role!).subscribe({
      next: () => {
        this.message.success('更新成功');
      },
      error: (err: any) => {
        this.message.error(err.error?.error?.message || '更新失败');
        this.loadMembers(this.currentAllianceId!);
      }
    });
  }
}

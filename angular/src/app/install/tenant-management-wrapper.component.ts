import { Component, ChangeDetectionStrategy, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { EditionService } from './edition.service';

@Component({
  selector: 'app-tenant-management',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule],
  template: `
    <div *ngIf="isBasicEdition" class="not-available">
      <nz-result nzStatus="403" nzTitle="功能不可用">
        <div nz-result-extra>
          <p>基础版不支持租户管理功能</p>
          <button nz-button nzType="primary" (click)="goHome()">返回首页</button>
        </div>
      </nz-result>
    </div>
    <router-outlet *ngIf="!isBasicEdition"></router-outlet>
  `,
  styles: [`
    .not-available {
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 400px;
    }
  `]
})
export class TenantManagementWrapperComponent implements OnInit {
  isBasicEdition = true;
  private editionService = inject(EditionService);
  private router = inject(Router);

  ngOnInit(): void {
    this.editionService.getEdition().subscribe(edition => {
      this.isBasicEdition = edition.edition === 'Basic';
      if (this.isBasicEdition) {
        // Don't redirect, just show the not-available message
      }
    });
  }

  goHome(): void {
    this.router.navigate(['/']);
  }
}
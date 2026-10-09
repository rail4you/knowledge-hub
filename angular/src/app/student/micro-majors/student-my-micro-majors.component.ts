import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import {
  MicroMajorService,
  MicroMajorEnrollmentStatus,
} from '../../micro-majors/micro-major.service';
import type {
  MyMicroMajorDto,
} from '../../micro-majors/micro-major.service';
import { StudentHeroComponent } from '../shared/student-hero/student-hero.component';
import { hashGradient } from '../../shared/utils/color.util';

@Component({
  selector: 'app-student-my-micro-majors',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    NzIconModule, NzSpinModule, NzModalModule, NzButtonModule,
    StudentHeroComponent,
  ],
  templateUrl: './student-my-micro-majors.component.html',
  styleUrls: ['./student-my-micro-majors.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StudentMyMicroMajorsComponent implements OnInit {
  private readonly microMajorService = inject(MicroMajorService);
  private readonly router = inject(Router);
  private readonly message = inject(NzMessageService);

  readonly items = signal<MyMicroMajorDto[]>([]);
  readonly loading = signal(true);
  readonly EnrollmentStatus = MicroMajorEnrollmentStatus;

  /** 证书预览弹窗：当前选中的已发证微专业（与后端 MicroMajorCertificate.CertificateImageUrl 同源） */
  readonly certPreviewItem = signal<MyMicroMajorDto | null>(null);
  readonly certPreviewVisible = signal(false);

  /** 头部数据总览（与卡片“微专业进度”三态对齐：已报名 / 已发证 / 未报名） */
  readonly heroStats = computed(() => {
    const items = this.items();
    const certCount = items.filter(i => i.enrollmentStatus === MicroMajorEnrollmentStatus.Certified).length;
    const inactiveCount = items.filter(i => i.enrollmentStatus === MicroMajorEnrollmentStatus.Cancelled).length;
    return [
      { label: '已报名', value: items.length - certCount - inactiveCount, suffix: '个', icon: 'play-circle', color: '#087f7a' },
      { label: '已发证', value: certCount, suffix: '个', icon: 'safety-certificate', color: '#f59e0b' },
      { label: '未报名', value: inactiveCount, suffix: '个', icon: 'stop', color: '#94a3b8' },
    ];
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.microMajorService.getMyMicroMajors().subscribe({
      next: result => {
        this.items.set(result || []);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.message.error('加载我的微专业失败');
      },
    });
  }

  /**
   * 微专业进度（学生端只展示三态）：
   * 已发证 = 已发证；未报名 = 已取消；其余（待审批/已通过/学习中/已完成）统一为已报名。
   * 后端的 Completed / InProgress 等是过程状态（含历史冻结数据），不在学生端展示。
   */
  getMicroMajorProgressLabel(status: number): string {
    if (status === MicroMajorEnrollmentStatus.Certified) return '已发证';
    if (status === MicroMajorEnrollmentStatus.Cancelled) return '未报名';
    return '已报名';
  }

  /** 是否有可查看的证书：已发证且后端存有证书图片（发证时前端画布合成上传） */
  hasCertificate(item: MyMicroMajorDto): boolean {
    return item.enrollmentStatus === MicroMajorEnrollmentStatus.Certified
      && !!item.certificateImageUrl?.trim();
  }

  /** 点击卡片上的证书图标：打开证书预览（阻止冒泡，避免跳详情页） */
  openCertificate(item: MyMicroMajorDto, event: Event): void {
    event.stopPropagation();
    event.preventDefault();
    if (!this.hasCertificate(item)) return;
    this.certPreviewItem.set(item);
    this.certPreviewVisible.set(true);
  }

  closeCertificate(): void {
    this.certPreviewVisible.set(false);
    this.certPreviewItem.set(null);
  }

  /** 下载文件名：微专业标题 + 证书 */
  certificateFileName(): string {
    const title = this.certPreviewItem()?.title?.trim() || '微专业';
    return `${title}-证书.png`;
  }

  openDetail(item: MyMicroMajorDto): void {
    if (!item.id) return;
    // 携带 from=my-micro-majors，详情页的“返回”会回到我的微专业（原路返回）
    this.router.navigate(['/student/micro-majors', item.id], { queryParams: { from: 'my-micro-majors' } });
  }

  coverGradient(item: MyMicroMajorDto): string {
    const palettes = ['#087f7a', '#086964', '#087f7a', '#087f7a', '#0891b2'];
    const key = item.title || item.id || '';
    return hashGradient(key, palettes);
  }

  hasCover(item: MyMicroMajorDto): boolean {
    return !!item.coverImageUrl && item.coverImageUrl.trim().length > 0;
  }
}

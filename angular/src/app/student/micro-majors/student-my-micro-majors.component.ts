import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { NzProgressModule } from 'ng-zorro-antd/progress';
import { NzEmptyModule } from 'ng-zorro-antd/empty';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzMessageService } from 'ng-zorro-antd/message';
import { MicroMajorService, MicroMajorEnrollmentStatus } from '../../micro-majors/micro-major.service';
import type { MyMicroMajorDto, MicroMajorCourseDto } from '../../micro-majors/micro-major.service';

@Component({
  selector: 'app-student-my-micro-majors',
  standalone: true,
  imports: [
    CommonModule, DatePipe, DecimalPipe, RouterModule,
    NzIconModule, NzSpinModule, NzProgressModule, NzEmptyModule, NzModalModule, NzButtonModule,
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

  readonly certificateVisible = signal(false);
  readonly activeCertificate = signal<MyMicroMajorDto | null>(null);

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

  openCertificate(item: MyMicroMajorDto): void {
    this.activeCertificate.set(item);
    this.certificateVisible.set(true);
  }

  closeCertificate(): void {
    this.certificateVisible.set(false);
    this.activeCertificate.set(null);
  }

  downloadCertificate(item?: MyMicroMajorDto): void {
    const cert = item ?? this.activeCertificate();
    if (!cert?.certificateImageUrl) return;

    const url = cert.certificateImageUrl;
    const extMatch = url.split('?')[0].match(/\.(png|jpe?g|webp|gif)$/i);
    const ext = extMatch ? extMatch[1].toLowerCase() : 'png';
    const safeTitle = (cert.title || '微专业证书').replace(/[\\/:*?"<>|]/g, '_');
    const filename = `微专业证书_${safeTitle}.${ext}`;

    fetch(url, { mode: 'cors' })
      .then(resp => {
        if (!resp.ok) throw new Error('download failed');
        return resp.blob();
      })
      .then(blob => {
        const objectUrl = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = objectUrl;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(objectUrl);
      })
      .catch(() => {
        // CORS 或网络失败时降级为新窗口打开原图，由用户手动保存
        window.open(url, '_blank');
        this.message.info('证书图片已在新窗口打开，可右键另存为保存');
      });
  }

  getStatusLabel(status: number): string {
    const labels: Record<number, string> = {
      [MicroMajorEnrollmentStatus.Pending]: '待审批',
      [MicroMajorEnrollmentStatus.Enrolled]: '已通过',
      [MicroMajorEnrollmentStatus.InProgress]: '学习中',
      [MicroMajorEnrollmentStatus.Completed]: '已完成',
      [MicroMajorEnrollmentStatus.Certified]: '已发证',
      [MicroMajorEnrollmentStatus.Cancelled]: '已取消',
    };
    return labels[status] || '未知';
  }

  getStatusIcon(status: number): string {
    const icons: Record<number, string> = {
      [MicroMajorEnrollmentStatus.Pending]: 'clock-circle',
      [MicroMajorEnrollmentStatus.Enrolled]: 'check-circle',
      [MicroMajorEnrollmentStatus.InProgress]: 'read',
      [MicroMajorEnrollmentStatus.Completed]: 'check-circle',
      [MicroMajorEnrollmentStatus.Certified]: 'safety-certificate',
      [MicroMajorEnrollmentStatus.Cancelled]: 'close-circle',
    };
    return icons[status] || 'question';
  }

  openDetail(item: MyMicroMajorDto): void {
    if (!item.id) return;
    // 携带 from=my-micro-majors，详情页的“返回”会回到我的微专业（原路返回）
    this.router.navigate(['/student/micro-majors', item.id], { queryParams: { from: 'my-micro-majors' } });
  }

  goCourse(course: MicroMajorCourseDto): void {
    this.router.navigate(['/student/courses', course.courseId]);
  }

  coverGradient(item: MyMicroMajorDto): string {
    const palettes = ['#1e6ce8', '#0c4cb8', '#2563eb', '#0284c7', '#0891b2'];
    const key = item.title || item.id || '';
    let hash = 0;
    for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) | 0;
    return palettes[Math.abs(hash) % palettes.length];
  }

  /** 学习进度保留 2 位小数 */
  roundProgress(value: number | null | undefined): number {
    if (value == null || isNaN(value)) return 0;
    return Math.round(value * 100) / 100;
  }
}

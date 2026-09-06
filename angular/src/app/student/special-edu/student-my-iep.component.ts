import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzDividerModule } from 'ng-zorro-antd/divider';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-student-my-iep',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, NzCardModule, NzDividerModule],
  template: `
  <nz-card nzTitle="我的 IEP 方案（只读）">
    @for (iep of items(); track iep.id) {
      <h3>{{ iep.studentName }} · {{ iep.categoryName }} · v{{ iep.versionNumber }}</h3>
      <p>{{ iep.profileSummary }}</p>
      <nz-divider nzText="长期目标"></nz-divider>
      @for (g of iep.longTermGoals; track g) { <p>• {{ g }}</p> }
      <nz-divider nzText="短期目标"></nz-divider>
      @for (g of iep.shortTermGoals; track g) { <p>• {{ g }}</p> }
      <nz-divider nzText="家校协同"></nz-divider>
      @for (g of iep.homeSchool; track g) { <p>• {{ g }}</p> }
      <nz-divider></nz-divider>
    } @empty {
      <p style="color:#999">暂无 IEP 数据，请联系教师。</p>
    }
  </nz-card>
  `,
})
export class StudentMyIepComponent {
  private http = inject(HttpClient);
  items = signal<any[]>([]);

  constructor() {
    this.http.get<any>('/api/learning/special-edu/ieps/mine', { params: { maxResultCount: '20' } as any })
      .subscribe({ next: (r: any) => this.items.set(r?.items ?? []), error: () => {} });
  }
}

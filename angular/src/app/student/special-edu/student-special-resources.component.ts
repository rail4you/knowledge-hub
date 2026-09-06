import { Component, ChangeDetectionStrategy, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzListModule } from 'ng-zorro-antd/list';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { SPECIAL_EDU_CATEGORIES } from '../../special-edu/special-edu.service';

@Component({
  selector: 'app-student-special-resources',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, NzCardModule, NzListModule, NzSelectModule],
  template: `
  <nz-card nzTitle="特教适配资源">
    <nz-select [(ngModel)]="category" (ngModelChange)="load()" nzAllowClear nzPlaceHolder="全部类别" style="width:200px;margin-bottom:12px">
      @for (c of categories; track c.value) { <nz-option [nzValue]="c.value" [nzLabel]="c.label"></nz-option> }
    </nz-select>
    <nz-list [nzDataSource]="items()" [nzRenderItem]="itemTpl">
      <ng-template #itemTpl let-item>
        <nz-list-item>
          <nz-list-item-meta [nzTitle]="item.title" [nzDescription]="item.modalityName + ' · ' + item.categoryName"></nz-list-item-meta>
          <div style="white-space:pre-wrap;max-width:60%">{{ item.contentText }}</div>
        </nz-list-item>
      </ng-template>
    </nz-list>
  </nz-card>
  `,
})
export class StudentSpecialResourcesComponent {
  private http = inject(HttpClient);
  categories = SPECIAL_EDU_CATEGORIES;
  category: number | null = null;
  items = signal<any[]>([]);

  constructor() {
    this.load();
  }

  load(): void {
    const params: any = { maxResultCount: '30' };
    if (this.category !== null && this.category !== undefined) params.category = String(this.category);
    this.http.get<any>('/api/learning/special-edu/resources', { params })
      .subscribe({ next: (r: any) => this.items.set(r?.items ?? []), error: () => {} });
  }
}

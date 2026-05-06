import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { EmploymentGuidanceSourceType, EmploymentService, EmploymentGuidanceRecordDto } from './employment.service';

@Component({
  selector: 'app-my-guidance',
  standalone: true,
  imports: [CommonModule, RouterLink, NzButtonModule, NzCardModule],
  templateUrl: './my-guidance.component.html',
  styleUrls: ['./my-guidance.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyGuidanceComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);

  readonly items = signal<EmploymentGuidanceRecordDto[]>([]);
  readonly sourceTypes = EmploymentGuidanceSourceType;

  ngOnInit(): void {
    this.employmentService.getMyGuidanceRecordList({
      skipCount: 0,
      maxResultCount: 100,
    }).subscribe(result => this.items.set(result.items || []));
  }

  getSourceLabel(type: EmploymentGuidanceSourceType): string {
    return type === EmploymentGuidanceSourceType.AI ? 'AI' : '人工';
  }
}

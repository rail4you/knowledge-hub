import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzModalModule } from 'ng-zorro-antd/modal';
import { NzTableModule } from 'ng-zorro-antd/table';
import { CreateUpdateStudentResumeDto, EmploymentService, StudentResumeDto } from './employment.service';

@Component({
  selector: 'app-my-resumes',
  standalone: true,
  imports: [CommonModule, FormsModule, NzButtonModule, NzCardModule, NzInputModule, NzModalModule, NzTableModule],
  templateUrl: './my-resumes.component.html',
  styleUrls: ['./my-resumes.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyResumesComponent implements OnInit {
  private readonly employmentService = inject(EmploymentService);
  private readonly message = inject(NzMessageService);

  readonly items = signal<StudentResumeDto[]>([]);
  modalVisible = false;
  editingId: string | null = null;
  form: CreateUpdateStudentResumeDto = this.createEmptyForm();

  ngOnInit(): void {
    this.reload();
  }

  createEmptyForm(): CreateUpdateStudentResumeDto {
    return {
      title: '',
      fullName: '',
      phoneNumber: '',
      email: '',
      schoolName: '',
      major: '',
      grade: '',
      className: '',
      studentNumber: '',
      summary: '',
      skills: '',
      educationExperience: '',
      internshipExperience: '',
      projectExperience: '',
      certificateText: '',
      attachmentUrl: '',
      isDefault: true,
    };
  }

  reload(): void {
    this.employmentService.getMyResumeList().subscribe(items => this.items.set(items || []));
  }

  openCreate(): void {
    this.editingId = null;
    this.form = this.createEmptyForm();
    this.modalVisible = true;
  }

  openEdit(item: StudentResumeDto): void {
    this.editingId = item.id;
    this.form = {
      title: item.title,
      fullName: item.fullName,
      phoneNumber: item.phoneNumber || '',
      email: item.email || '',
      schoolName: item.schoolName || '',
      major: item.major || '',
      grade: item.grade || '',
      className: item.className || '',
      studentNumber: item.studentNumber || '',
      summary: item.summary || '',
      skills: item.skills || '',
      educationExperience: item.educationExperience || '',
      internshipExperience: item.internshipExperience || '',
      projectExperience: item.projectExperience || '',
      certificateText: item.certificateText || '',
      attachmentUrl: item.attachmentUrl || '',
      isDefault: item.isDefault,
    };
    this.modalVisible = true;
  }

  save(): void {
    const request = this.editingId
      ? this.employmentService.updateResume(this.editingId, this.form)
      : this.employmentService.createResume(this.form);

    request.subscribe({
      next: () => {
        this.modalVisible = false;
        this.message.success('简历已保存');
        this.reload();
      },
      error: () => this.message.error('保存失败'),
    });
  }

  setDefault(id: string): void {
    this.employmentService.setDefaultResume(id).subscribe({
      next: () => {
        this.message.success('已设为默认简历');
        this.reload();
      },
      error: () => this.message.error('设置失败'),
    });
  }

  delete(id: string): void {
    this.employmentService.deleteResume(id).subscribe({
      next: () => {
        this.message.success('简历已删除');
        this.reload();
      },
      error: () => this.message.error('删除失败'),
    });
  }
}

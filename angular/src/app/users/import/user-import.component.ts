import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { NzButtonModule } from 'ng-zorro-antd/button';
import { NzCardModule } from 'ng-zorro-antd/card';
import { NzFormModule } from 'ng-zorro-antd/form';
import { NzInputModule } from 'ng-zorro-antd/input';
import { NzMessageService } from 'ng-zorro-antd/message';
import { NzUploadModule, NzUploadFile, NzUploadChangeParam } from 'ng-zorro-antd/upload';
import { NzTableModule } from 'ng-zorro-antd/table';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzResultModule } from 'ng-zorro-antd/result';
import { NzSpinModule } from 'ng-zorro-antd/spin';
import { UserImportService } from '../../proxy/users/user-import.service';

@Component({
  selector: 'app-user-import',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    NzButtonModule,
    NzCardModule,
    NzFormModule,
    NzInputModule,
    NzUploadModule,
    NzTableModule,
    NzIconModule,
    NzResultModule,
    NzSpinModule,
  ],
  templateUrl: './user-import.html',
  styleUrls: ['./user-import.scss'],
})
export class UserImportComponent {
  private fb = inject(FormBuilder);
  private message = inject(NzMessageService);
  private userImportService = inject(UserImportService);

  fileList: NzUploadFile[] = [];
  uploading = false;
  uploadSuccess = false;
  importResult: any = null;

  beforeUpload = (file: NzUploadFile): boolean => {
    this.fileList = [file];
    return false;
  };

  handleUpload(): void {
    const file = this.fileList[0];
    if (!file) {
      this.message.warning('请选择要导入的Excel文件');
      return;
    }

    this.uploading = true;

    const reader = new FileReader();
    reader.readAsArrayBuffer(file as any);
    reader.onload = () => {
      const arrayBuffer = reader.result as ArrayBuffer;
      const uint8Array = new Uint8Array(arrayBuffer);

      this.userImportService
        .importUsingUrl(uint8Array as any)
        .subscribe({
          next: (result) => {
            this.uploading = false;
            this.uploadSuccess = true;
            this.importResult = result;
            this.message.success(
              `导入完成：成功 ${result.successCount} 条，失败 ${result.failCount} 条`
            );
          },
          error: (err) => {
            this.uploading = false;
            this.message.error('导入失败: ' + err.message);
          },
        });
    };
  }

  downloadTemplate(): void {
    const link = document.createElement('a');
    link.href = '/assets/templates/user-import-template.xlsx';
    link.download = '用户导入模板.xlsx';
    link.click();
  }

  reset(): void {
    this.fileList = [];
    this.uploadSuccess = false;
    this.importResult = null;
  }
}

import { Component, ChangeDetectionStrategy, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { NzIconModule } from 'ng-zorro-antd/icon';
import { NzAvatarModule } from 'ng-zorro-antd/avatar';
import { NzDropDownModule } from 'ng-zorro-antd/dropdown';
import { AuthService } from '@abp/ng.core';

@Component({
  selector: 'app-student-layout',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    NzIconModule,
    NzAvatarModule,
    NzDropDownModule
  ],
  templateUrl: './student-layout.component.html',
  styleUrls: ['./student-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class StudentLayoutComponent implements OnInit {
  private authService = inject(AuthService);
  private router = inject(Router);

  userName = signal('用户');

  ngOnInit() {
    const storedUser = localStorage.getItem('abp_session_state');
    if (storedUser) {
      try {
        const session = JSON.parse(storedUser);
        if (session?.username) {
          this.userName.set(session.username);
        }
      } catch {
        // ignore parse errors
      }
    }
  }

  get userInitial(): string {
    return this.userName()?.charAt(0)?.toUpperCase() || 'U';
  }

  logout() {
    this.authService.logout().subscribe(() => {
      this.router.navigate(['/']);
    });
  }
}

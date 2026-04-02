import { Component, inject, OnInit, signal, HostListener } from '@angular/core';
import { UntypedFormBuilder, UntypedFormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { RouterLink, ActivatedRoute } from '@angular/router';
import {
  AuthService,
  ConfigStateService,
  SessionStateService,
  LocalizationPipe,
} from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { catchError, finalize } from 'rxjs/operators';
import { throwError } from 'rxjs';

@Component({
  selector: 'app-login',
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.scss'],
  imports: [ReactiveFormsModule, RouterLink, LocalizationPipe],
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(UntypedFormBuilder);
  private readonly authService = inject(AuthService);
  private readonly configState = inject(ConfigStateService);
  private readonly sessionState = inject(SessionStateService);
  private readonly toasterService = inject(ToasterService);
  private readonly route = inject(ActivatedRoute);

  form!: UntypedFormGroup;
  inProgress = false;
  showPassword = signal(false);
  langDropdownOpen = signal(false);
  languages: { cultureName: string; displayName: string; flagIcon: string }[] = [];
  currentLang = '';

  ngOnInit() {
    this.buildForm();
    this.loadLanguages();
  }

  buildForm() {
    this.form = this.fb.group({
      username: ['', [Validators.required, Validators.maxLength(255)]],
      password: ['', [Validators.required, Validators.maxLength(128)]],
      rememberMe: [false],
    });
  }

  loadLanguages() {
    this.configState
      .getDeep$('localization.languages')
      .subscribe((langs: any[]) => {
        if (langs) {
          this.languages = langs.map((l: any) => ({
            cultureName: l.cultureName,
            displayName: l.displayName,
            flagIcon: l.flagIcon,
          }));
        }
      });
    this.currentLang = this.sessionState.getLanguage() || 'zh-Hans';
  }

  getCurrentLangDisplayName(): string {
    const lang = this.languages.find((l) => l.cultureName === this.currentLang);
    return lang ? lang.displayName : this.currentLang;
  }

  onChangeLang(cultureName: string) {
    this.sessionState.setLanguage(cultureName);
    this.currentLang = cultureName;
    this.langDropdownOpen.set(false);
  }

  toggleLangDropdown() {
    this.langDropdownOpen.update((v) => !v);
  }

  @HostListener('document:click', ['$event.target'])
  onDocumentClick(target: HTMLElement) {
    const selector = target.closest('.language-selector');
    if (!selector) {
      this.langDropdownOpen.set(false);
    }
  }

  togglePassword() {
    this.showPassword.update((v) => !v);
  }

  onSubmit() {
    if (this.form.invalid) return;

    this.inProgress = true;
    const { username, password, rememberMe } = this.form.value;
    const redirectUrl =
      this.route.snapshot.queryParams['returnUrl'] || '/';

    this.authService
      .login({ username, password, rememberMe, redirectUrl })
      .pipe(
        catchError((err: any) => {
          this.toasterService.error(
            err?.error?.error_description ||
              err?.error?.error?.message ||
              '::DefaultErrorMessage',
            '',
            { life: 7000 }
          );
          return throwError(() => err);
        }),
        finalize(() => (this.inProgress = false))
      )
      .subscribe();
  }
}

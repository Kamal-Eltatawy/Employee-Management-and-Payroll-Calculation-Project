import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiError } from '../http/api-error';
import { FieldErrorsComponent } from '../../shared/ui/field-errors.component';
import { IconComponent } from '../../shared/ui/icon.component';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, FieldErrorsComponent, IconComponent],
  template: `
    <div class="login">
      <section class="login-brand">
        <span class="brand-mark">P</span>
        <h1 i18n="@@login.heading">Payroll</h1>
        <p i18n="@@login.pitch">Employees, allowances and deductions, and exact monthly payslips.</p>
      </section>
      <form class="surface login-form" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <h2 class="h4 mb-1" i18n="@@login.title">Sign in</h2>
        <p class="text-body-secondary mb-4" i18n="@@login.subtitle">Use your work email and password.</p>
        @if (error()) {
          <div class="alert alert-danger" role="alert">{{ error() }}</div>
        }
        <div class="mb-3">
          <label class="form-label" for="email" i18n="@@field.email">Email</label>
          <input id="email" class="form-control ltr" type="email" formControlName="email" autocomplete="username"
                 [class.is-invalid]="form.controls.email.invalid && form.controls.email.touched" />
          <app-field-errors [control]="form.controls.email" label="Email" i18n-label="@@field.email" />
        </div>
        <div class="mb-4">
          <label class="form-label" for="password" i18n="@@field.password">Password</label>
          <div class="password-field">
            <input id="password" class="form-control" [type]="showPassword() ? 'text' : 'password'" formControlName="password"
                   autocomplete="current-password"
                   [class.is-invalid]="form.controls.password.invalid && form.controls.password.touched" />
            <button type="button" class="reveal" (click)="showPassword.set(!showPassword())" [attr.aria-pressed]="showPassword()"
                    [attr.aria-label]="showPassword() ? hideLabel : showLabel" [title]="showPassword() ? hideLabel : showLabel">
              <app-icon [name]="showPassword() ? 'eyeOff' : 'eye'" />
            </button>
          </div>
          <app-field-errors [control]="form.controls.password" label="Password" i18n-label="@@field.password" />
        </div>
        <button type="submit" class="btn btn-primary w-100" [disabled]="busy()">
          @if (busy()) {
            <ng-container i18n="@@login.busy">Signing in…</ng-container>
          } @else {
            <ng-container i18n="@@login.submit">Sign in</ng-container>
          }
        </button>
      </form>
    </div>
  `,
  styles: `
    .login { min-height: calc(100vh - 4rem); display: grid; grid-template-columns: 1fr minmax(320px, 420px); gap: 3rem;
      align-items: center; max-width: 960px; margin: 0 auto; }
    .login-brand h1 { font-size: 2.25rem; margin: 1rem 0 .5rem; }
    .login-brand p { color: var(--app-muted); font-size: 1.1rem; max-width: 28rem; }
    .brand-mark { display: grid; place-items: center; width: 3rem; height: 3rem; border-radius: .8rem; color: #fff;
      font-weight: 700; font-size: 1.25rem; background: linear-gradient(135deg, #3b82c4, #1f4e79); }
    .login-form { padding: 2rem; }
    /* Our own show/hide button replaces the browser's (Edge) so it never overlaps the validation state, in LTR and RTL. */
    .password-field { position: relative; }
    .password-field input { padding-inline-end: 2.75rem; background-image: none; }
    .password-field input::-ms-reveal, .password-field input::-ms-clear { display: none; }
    .reveal { position: absolute; inset-block-start: 50%; inset-inline-end: .5rem; transform: translateY(-50%);
      display: grid; place-items: center; width: 2rem; height: 2rem; padding: 0; border: 0; border-radius: .4rem;
      background: none; color: var(--app-muted); }
    .reveal:hover, .reveal:focus-visible { color: var(--bs-primary); background: #f2f4f7; }
    @media (max-width: 767.98px) { .login { grid-template-columns: 1fr; gap: 1.5rem; } }
  `,
})
export class LoginPage {
  /** Query parameter set by authGuard. */
  readonly returnUrl = input<string>();

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly showLabel = $localize`:@@login.showPassword:Show password`;
  protected readonly hideLabel = $localize`:@@login.hidePassword:Hide password`;
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required]],
    password: ['', [Validators.required]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(email, password).subscribe({
      next: () => {
        this.busy.set(false);
        const returnUrl = this.returnUrl();
        // Only same-app paths: never redirect to another site.
        const target = returnUrl?.startsWith('/') && !returnUrl.startsWith('//') ? returnUrl : this.auth.homeUrl();
        void this.router.navigateByUrl(target);
      },
      error: (error: ApiError) => {
        this.busy.set(false);
        this.error.set(
          error.status === 401 ? $localize`:@@login.invalid:Invalid email or password.` : (error.detail ?? error.title),
        );
      },
    });
  }
}

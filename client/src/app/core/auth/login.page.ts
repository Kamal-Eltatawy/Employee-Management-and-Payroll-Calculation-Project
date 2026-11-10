import { Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiError } from '../http/api-error';
import { FieldErrorsComponent } from '../../shared/ui/field-errors.component';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, FieldErrorsComponent],
  template: `
    <div class="row justify-content-center">
      <div class="col-md-6 col-lg-4">
        <form class="card shadow-sm" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="card-body">
            <h1 class="h4 mb-3">Sign in</h1>
            @if (error()) {
              <div class="alert alert-danger" role="alert">{{ error() }}</div>
            }
            <div class="mb-3">
              <label class="form-label" for="email">Email</label>
              <input id="email" class="form-control" type="email" formControlName="email" autocomplete="username"
                     [class.is-invalid]="form.controls.email.invalid && form.controls.email.touched" />
              <app-field-errors [control]="form.controls.email" label="Email" />
            </div>
            <div class="mb-3">
              <label class="form-label" for="password">Password</label>
              <input id="password" class="form-control" type="password" formControlName="password" autocomplete="current-password"
                     [class.is-invalid]="form.controls.password.invalid && form.controls.password.touched" />
              <app-field-errors [control]="form.controls.password" label="Password" />
            </div>
            <button type="submit" class="btn btn-primary w-100" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>
          </div>
        </form>
      </div>
    </div>
  `,
})
export class LoginPage {
  /** Query parameter set by authGuard. */
  readonly returnUrl = input<string>();

  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
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
        this.error.set(error.status === 401 ? 'Invalid email or password.' : (error.detail ?? error.title));
      },
    });
  }
}

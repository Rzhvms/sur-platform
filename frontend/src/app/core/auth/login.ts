import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, map } from 'rxjs';
import { text } from '../../shared/text';
import { errorMessage } from '../http/api';
import { homeFor } from './access';
import { internalDestination } from './session-policy';
import { Session } from './session';

@Component({
  selector: 'sur-login',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="login-card">
      <div class="brand-mark login-mark" aria-hidden="true">{{ t.brand }}</div>
      <h1>{{ t.product }}</h1>
      @if (passwordChanged()) { <p role="status">{{ t.passwordChanged }}</p> }
      @if (session.logoutUnconfirmed()) {
        <div role="status" class="auth-notice">
          <p>{{ t.logoutNotConfirmed }}</p>
          <button type="button" [disabled]="pending()" (click)="retryLogout()">{{ t.retryLogout }}</button>
        </div>
      }
      @if (notice()) { <p class="muted" role="status">{{ notice() }}</p> }
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>{{ t.email }}<input formControlName="email" type="email" autocomplete="username" required></label>
        <label>
          {{ t.password }}
          <span class="password-field">
            <input formControlName="password" [type]="showPassword() ? 'text' : 'password'" autocomplete="current-password" required>
            <button type="button" class="password-toggle" (click)="togglePassword()" [attr.aria-pressed]="showPassword()" [attr.aria-label]="showPassword() ? t.hidePassword : t.showPassword">
              @if (showPassword()) {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23"/></svg>
              } @else {
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
              }
            </button>
          </span>
        </label>
        @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
        <button class="primary" [disabled]="pending() || formStatus() !== 'VALID'">{{ pending() ? t.loading : t.login }}</button>
      </form>
    </section>
  `,
})
export class Login {
  protected readonly t = text;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(256)]],
  });

  protected readonly formStatus = toSignal(this.form.statusChanges, { initialValue: this.form.status });
  protected readonly pending = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal('');
  protected readonly notice = signal('');
  protected readonly session = inject(Session);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly passwordChanged = toSignal(this.route.queryParamMap.pipe(
    map(params => params.get('changed') === '1'),
  ), { initialValue: false });

  constructor() {
    if (this.route.snapshot.queryParamMap.get('restore') === 'failed') {
      this.notice.set(navigator.locks ? text.unavailable : text.unsupportedBrowser);
    }
  }

  protected retryLogout(): void {
    this.pending.set(true);
    this.session.logout().pipe(finalize(() => this.pending.set(false))).subscribe();
  }

  protected togglePassword(): void {
    this.showPassword.update(value => !value);
  }

  protected submit(): void {
    if (this.form.invalid || this.pending()) {
      return;
    }
    this.pending.set(true);
    this.error.set('');
    this.session.login(this.form.getRawValue()).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        this.form.controls.password.reset();
        this.pending.set(false);
      }),
    ).subscribe({
      next: () => {
        const target = internalDestination(this.route.snapshot.queryParamMap.get('next'));
        const destination = this.session.restricted() ? '/change-password' : target ?? homeFor(this.session.rights());
        void this.router.navigateByUrl(destination, { replaceUrl: true });
      },
      error: (error: unknown) => this.error.set(
        error instanceof HttpErrorResponse && error.status === 401 ? text.loginError : errorMessage(error),
      ),
    });
  }
}

import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { text } from '../../shared/text';
import { errorMessage } from '../http/api';
import { Session } from './session';

@Component({
  selector: 'sur-change-password',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="login-card">
      <div class="brand-mark login-mark" aria-hidden="true">{{ t.brand }}</div>
      <h1>{{ t.changePassword }}</h1>
      <p class="muted">{{ t.passwordChangeRequired }}</p>
      <form [formGroup]="form" (ngSubmit)="submit()">
        <label>{{ t.oldPassword }}<input formControlName="oldPassword" type="password" autocomplete="current-password" required maxlength="256"></label>
        <label>{{ t.newPassword }}<input formControlName="newPassword" type="password" autocomplete="new-password" required maxlength="256" aria-describedby="password-length"></label>
        <span class="muted" id="password-length">{{ t.passwordLength }}</span>
        <label>{{ t.repeatPassword }}<input formControlName="confirmation" type="password" autocomplete="new-password" required maxlength="256"></label>
        @if (error()) { <p class="form-error" role="alert">{{ error() }}</p> }
        <button class="primary" [disabled]="pending() || formStatus() !== 'VALID'">{{ pending() ? t.loading : t.changePassword }}</button>
      </form>
      <button type="button" [disabled]="pending()" (click)="logout()">{{ t.logout }}</button>
    </section>
  `,
})
export class ChangePassword {
  protected readonly t = text;
  private readonly passwordRules = [Validators.required, Validators.minLength(8), Validators.maxLength(256)];
  protected readonly form = inject(FormBuilder).nonNullable.group({
    oldPassword: ['', this.passwordRules],
    newPassword: ['', this.passwordRules],
    confirmation: ['', this.passwordRules],
  });
  protected readonly formStatus = toSignal(this.form.statusChanges, { initialValue: this.form.status });
  protected readonly pending = signal(false);
  protected readonly error = signal('');
  private readonly session = inject(Session);
  private readonly destroyRef = inject(DestroyRef);

  /** Бизнес-отказ приходит с HTTP 200 и isSuccess=false. */
  protected submit(): void {
    if (this.form.invalid || this.pending()) {
      return;
    }
    const { oldPassword, newPassword, confirmation } = this.form.getRawValue();
    if (newPassword !== confirmation) {
      this.error.set(text.passwordMismatch);
      return;
    }

    this.pending.set(true);
    this.error.set('');
    const token = this.session.token();
    this.session.changePassword(oldPassword, newPassword).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => {
        this.form.reset();
        this.pending.set(false);
      }),
    ).subscribe({
      next: success => {
        if (!success) {
          this.error.set(text.passwordRejected);
        }
      },
      error: (error: unknown) => {
        if (token && error instanceof HttpErrorResponse && error.status === 401) {
          this.session.expire(token);
          return;
        }
        const uncertain = error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500);
        this.error.set(uncertain ? text.passwordUnknown : errorMessage(error));
      },
    });
  }

  protected logout(): void {
    this.session.logout().subscribe();
  }
}

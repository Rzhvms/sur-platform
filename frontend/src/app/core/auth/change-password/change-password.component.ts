import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { text } from '../../../shared/text';
import { errorMessage } from '../../http/api';
import { SessionService } from '../session.service';

@Component({
  selector: 'sur-change-password',
  imports: [ReactiveFormsModule],
  templateUrl: './change-password.component.html',
})
export class ChangePasswordComponent {
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
  private readonly session = inject(SessionService);
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

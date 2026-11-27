import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize, map } from 'rxjs';
import { text } from '../../../shared/text';
import { errorMessage } from '../../http/api';
import { homeFor } from '../access';
import { internalDestination } from '../session-policy';
import { SessionService } from '../session.service';

@Component({
  selector: 'sur-login',
  imports: [ReactiveFormsModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
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
  protected readonly session = inject(SessionService);
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

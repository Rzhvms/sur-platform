import { DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, finalize, map } from 'rxjs';
import { guid } from '../risks/model';
import { DocumentJob, Operation, operation } from './document-contract';
import { documentError } from './documents-api';

export class DocumentRequest<T extends object> {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  readonly id = toSignal(this.route.queryParamMap.pipe(map(params => {
    const key = params.get('job');
    return key && guid.test(key) ? key.toLowerCase() : null;
  })), { initialValue: null });
  readonly attempt = signal<Operation<T> | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');

  send(params: T, submit: (request: Operation<T>) => Observable<DocumentJob>, accepted?: (job: DocumentJob) => void): void {
    if (this.busy()) { return; }
    const attempt = this.attempt() ?? operation(params);
    this.attempt.set(attempt);
    this.busy.set(true);
    this.error.set('');
    submit(attempt).pipe(
      takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false)),
    ).subscribe({
      next: job => {
        accepted?.(job);
        this.attempt.set(null);
        void this.router.navigate([], { relativeTo: this.route, queryParams: { job: job.id } });
      },
      error: error => this.error.set(documentError(error, true)),
    });
  }

  reset(): void {
    this.attempt.set(null);
    this.error.set('');
    if (this.id()) {
      void this.router.navigate([], { relativeTo: this.route, queryParams: { job: null } });
    }
  }
}

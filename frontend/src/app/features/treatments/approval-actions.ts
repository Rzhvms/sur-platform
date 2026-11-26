import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { Session } from '../../core/auth/session';
import { problemCode } from '../../core/http/api';
import { text } from '../../shared/text';
import { Approval, Decision, decisionReason, treatmentText } from './treatment-contract';
import { TreatmentsApi, treatmentWriteError } from './treatments-api';

@Component({
  selector: 'sur-approval-actions',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (!selection()) {
    <div class="approval-actions">
      @if (inQueue() && approval().status === 'PENDING') {
        <button type="button" class="primary" [disabled]="busy()" (click)="choose('approve')">{{ w.approve }}</button>
        <button type="button" class="approval-negative" [disabled]="busy()" (click)="choose('reject')">{{ w.reject }}</button>
      }
      @if (session.hasRight('admin')) {
        <button type="button" [disabled]="busy()" (click)="choose('revoke')">{{ w.revoke }}</button>
      }
    </div>
    }
    @if (selection(); as choice) {
      <form class="form-stack approval-confirmation" [formGroup]="form" (ngSubmit)="submit()" aria-labelledby="approval-confirm-heading-{{ approval().id }}">
        <h3 id="approval-confirm-heading-{{ approval().id }}">{{ w[choice.action] }}</h3>
        <p>{{ choice.action === 'approve' ? w.approveConfirm : choice.action === 'reject' ? w.rejectConfirm : w.revokeConfirm }}</p>
        @if (choice.action !== 'approve') {
          <label>{{ w.reason }} <textarea [formControl]="reason" required></textarea></label>
        }
        @if (confirmation()) {
          <p role="status">{{ w.discardDrafts }}</p>
          <label><input type="checkbox" [formControl]="confirmed" /> {{ w.confirmTransition }}</label>
        }
        <div class="approval-actions">
          <button class="primary" type="submit" [disabled]="busy() || (confirmation() && !confirmed.value)">{{ busy() ? t.loading : w.confirmDecision }}</button>
          <button type="button" [disabled]="busy()" (click)="cancel()">{{ t.cancel }}</button>
        </div>
      </form>
    }
    @if (notice()) { <p class="form-error" role="alert">{{ notice() }}</p> }
  `,
})
export class ApprovalActions {
  readonly approval = input.required<Approval>();
  readonly inQueue = input(false);
  readonly changed = output<string>();
  private readonly api = inject(TreatmentsApi);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly session = inject(Session);
  protected readonly t = text;
  protected readonly w = treatmentText;
  protected readonly selection = signal<{ action: Decision; approval: Approval } | null>(null);
  protected readonly busy = signal(false);
  protected readonly notice = signal('');
  protected readonly confirmation = signal(false);
  protected readonly confirmed = new FormControl(false, { nonNullable: true });
  protected readonly reason = new FormControl('', { nonNullable: true });
  /** Директива формы связывает нативный submit с Angular ngSubmit */
  protected readonly form = new FormGroup({ reason: this.reason, confirmed: this.confirmed });

  /** Фиксируем версию запроса, которую пользователь изучил перед решением. */
  protected choose(action: Decision): void {
    this.selection.set({ action, approval: this.approval() });
    this.reason.reset();
    this.confirmed.reset();
    this.confirmation.set(false);
    this.notice.set('');
  }

  /** Закрывает подтверждение вместе с сообщением валидации */
  protected cancel(): void {
    this.selection.set(null);
    this.notice.set('');
    this.confirmation.set(false);
  }

  protected submit(): void {
    const selected = this.selection();
    if (!selected || this.busy() || (this.confirmation() && !this.confirmed.value)) {
      return;
    }
    const reason = decisionReason(selected.action, this.reason.value);
    if (reason === null) {
      this.notice.set(treatmentText.reasonRequired);
      return;
    }

    this.busy.set(true);
    this.api.decide(selected.approval, selected.action, reason || undefined, this.confirmed.value).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)),
    ).subscribe({
      next: () => this.finish(treatmentText.saved),
      error: (error: unknown) => {
        if (error instanceof HttpErrorResponse && problemCode(error) === 'TREATMENT_CONFIRMATION_REQUIRED') {
          this.confirmation.set(true);
        } else {
          this.finish(treatmentWriteError(error));
        }
      },
    });
  }

  private finish(message: string): void {
    this.selection.set(null);
    this.changed.emit(message);
  }
}

import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
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
    <div class="action-row">
      @if (inQueue() && approval().status === 'PENDING') {
        <button type="button" [disabled]="busy()" (click)="choose('approve')">{{ w.approve }}</button>
        <button type="button" [disabled]="busy()" (click)="choose('reject')">{{ w.reject }}</button>
      }
      @if (session.hasRight('admin')) {
        <button type="button" [disabled]="busy()" (click)="choose('revoke')">{{ w.revoke }}</button>
      }
    </div>
    @if (selection(); as choice) {
      <form class="form-stack" (ngSubmit)="submit()">
        @if (choice.action !== 'approve') {
          <label>{{ w.reason }} <textarea [formControl]="reason" required></textarea></label>
        }
        @if (confirmation()) {
          <p role="status">{{ w.discardDrafts }}</p>
          <label><input type="checkbox" [formControl]="confirmed" /> {{ w.confirmTransition }}</label>
        }
        <div class="action-row">
          <button class="primary" type="submit" [disabled]="busy()">{{ w[choice.action] }}</button>
          <button type="button" [disabled]="busy()" (click)="selection.set(null)">{{ t.cancel }}</button>
        </div>
      </form>
    }
    @if (notice()) { <p role="status">{{ notice() }}</p> }
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

  /** Фиксируем версию запроса, которую пользователь изучил перед решением. */
  protected choose(action: Decision): void {
    this.selection.set({ action, approval: this.approval() });
    this.reason.reset();
    this.confirmed.reset();
    this.confirmation.set(false);
    this.notice.set('');
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

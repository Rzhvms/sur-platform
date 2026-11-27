import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { SessionService } from '../../../core/auth/session.service';
import { problemCode } from '../../../core/http/api';
import { text } from '../../../shared/text';
import { Approval, Decision, decisionReason, treatmentText } from '../treatment-contract';
import { TreatmentsApiService, treatmentWriteError } from '../treatments-api.service';

@Component({
  selector: 'sur-approval-actions',
  imports: [ReactiveFormsModule],
  templateUrl: './approval-actions.component.html',
  styleUrl: './approval-actions.component.scss',
})
export class ApprovalActionsComponent {
  readonly approval = input.required<Approval>();
  readonly inQueue = input(false);
  readonly changed = output<string>();
  private readonly api = inject(TreatmentsApiService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly session = inject(SessionService);
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

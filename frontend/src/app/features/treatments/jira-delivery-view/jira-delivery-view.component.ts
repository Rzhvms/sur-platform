import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { SessionService } from '../../../core/auth/session.service';
import { utcTime } from '../../../shared/format';
import { JiraDelivery, jiraLink, jiraResolution, treatmentText } from '../treatment-contract';
import { TreatmentsApiService, treatmentWriteError } from '../treatments-api.service';

@Component({
  selector: 'sur-jira-delivery',
  imports: [ReactiveFormsModule],
  templateUrl: './jira-delivery-view.component.html',
})
export class JiraDeliveryViewComponent {
  readonly treatmentId = input.required<string>();
  readonly jira = input.required<JiraDelivery>();
  readonly changed = output<void>();
  private readonly api = inject(TreatmentsApiService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly session = inject(SessionService);
  protected readonly w = treatmentText;
  protected readonly time = utcTime;
  protected readonly busy = signal(false);
  protected readonly notice = signal('');
  protected readonly reviewed = signal<string | null>(null);
  protected readonly issue = new FormControl('', { nonNullable: true });
  protected readonly absent = new FormControl(false, { nonNullable: true });
  protected readonly issueLink = computed(() => this.jira().state === 'LINKED' ? jiraLink(this.jira().issueUrl) : null);
  protected readonly stateMessage = computed(() => {
    switch (this.jira().state) {
      case 'PENDING': return treatmentText.jiraPending;
      case 'SENDING': return treatmentText.jiraSending;
      case 'DEFERRED': return treatmentText.jiraDeferred;
      case 'UNKNOWN': return treatmentText.jiraUnknown;
      default: return null;
    }
  });

  /** Привязываем ввод к показанной попытке с точностью серверной строки времени. */
  protected begin(attempt: string): void {
    this.issue.reset();
    this.absent.reset();
    this.notice.set('');
    this.reviewed.set(attempt);
  }

  protected resolve(absent: boolean): void {
    if (this.busy() || !this.session.hasRight('admin') || (absent && !this.absent.value)) {
      return;
    }
    const result = jiraResolution(this.jira(), this.reviewed(), this.issue.value, absent);
    if (!result) {
      return;
    }
    this.busy.set(true);
    this.notice.set('');
    this.api.resolve(this.treatmentId(), result.attemptStartedAt, result.issue, result.confirmAbsent).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)),
    ).subscribe({
      next: () => {
        this.reviewed.set(null);
        this.notice.set(treatmentText.saved);
        this.changed.emit();
      },
      error: (error: unknown) => {
        this.reviewed.set(null);
        this.notice.set(error instanceof HttpErrorResponse && error.status === 409
          ? treatmentText.resolutionConflict : treatmentWriteError(error));
        this.changed.emit();
      },
    });
  }
}

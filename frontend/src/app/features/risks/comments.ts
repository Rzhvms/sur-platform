import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, input, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, finalize, map, of, startWith, switchMap } from 'rxjs';
import { Session } from '../../core/auth/session';
import { errorMessage } from '../../core/http/api';
import { utcTime } from '../../shared/format';
import { text } from '../../shared/text';
import { Confirm } from '../../shared/ui/confirm';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { RiskComment } from './model';
import { RisksApi, historyPageSize } from './risks-api';

const loading: DataState<RiskComment[]> = { kind: 'loading' };

@Component({
  selector: 'sur-risk-comments',
  imports: [ReactiveFormsModule, Confirm, DataStateView],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './comments.html',
})
export class RiskComments {
  readonly subject = input<'risks' | 'treatments'>('risks');
  readonly riskId = input.required<string>();
  private readonly api = inject(RisksApi);
  private readonly session = inject(Session);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required(Confirm);
  private readonly removal = signal<RiskComment | null>(null);
  private readonly sequence = signal(0);
  protected readonly t = text;
  protected readonly utcTime = utcTime;
  protected readonly pageSize = historyPageSize;
  protected readonly offset = signal(0);
  protected readonly error = signal('');
  protected readonly pending = signal(false);
  protected readonly conflict = signal(false);
  protected readonly edit = signal<RiskComment | null>(null);
  protected readonly message = new FormControl('', {
    nonNullable: true, validators: [Validators.required, Validators.maxLength(16000)],
  });
  protected readonly messageText = toSignal(this.message.valueChanges, { initialValue: '' });
  protected readonly canComment = computed(() => this.session.hasRight(this.subject() === 'risks' ? 'comment_risk' : 'comment_treatment'));
  private readonly query = computed(() => ({ id: this.riskId(), subject: this.subject(), offset: this.offset(), sequence: this.sequence() }));
  protected readonly result = toSignal(toObservable(this.query).pipe(switchMap(({ id, offset, subject }) => this.api.comments(id, offset, subject).pipe(
    map((value): DataState<RiskComment[]> => ({ kind: 'ready', value })),
    catchError((error: unknown) => of<DataState<RiskComment[]>>({ kind: 'error', message: errorMessage(error) })),
    startWith(loading),
  ))), { initialValue: loading });

  protected caption(item: RiskComment): string {
    if (item.isSystem) {
      return text.systemComment;
    }
    return item.authorId === this.session.userId() ? text.ownComment : text.userComment;
  }

  /** Интерфейс повторяет правила видимости. Запись и версию подтверждает сервер. */
  protected canEdit(item: RiskComment): boolean {
    return this.canComment() && !item.isSystem
      && (item.authorId === this.session.userId() || this.session.rights().includes('admin'));
  }

  protected page(direction: number): void {
    this.offset.update(offset => Math.max(0, offset + direction * historyPageSize));
  }

  protected refresh(): void {
    this.sequence.update(sequence => sequence + 1);
  }

  protected startEdit(item: RiskComment): void {
    if (!this.pending() && this.canEdit(item)) {
      this.edit.set(item);
      this.message.setValue(item.text);
      this.conflict.set(false);
      this.error.set('');
    }
  }

  protected cancelEdit(): void {
    this.edit.set(null);
    this.message.reset();
    this.conflict.set(false);
    this.error.set('');
  }

  protected acceptVersion(item: RiskComment): void {
    if (this.edit()?.id === item.id && this.canEdit(item)) {
      this.edit.set(item);
      this.conflict.set(false);
      this.error.set('');
    }
  }

  protected submit(): void {
    const edit = this.edit();
    const value = this.message.value.trim();
    if (this.pending() || this.conflict() || this.message.invalid || !value || !this.canComment() || (edit && !this.canEdit(edit))) {
      return;
    }
    this.pending.set(true);
    this.error.set('');
    this.api.saveComment(this.riskId(), value, edit ?? undefined, this.subject()).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.pending.set(false)),
    ).subscribe({
      next: () => {
        this.cancelEdit();
        this.refresh();
      },
      error: (error: unknown) => this.reportError(error, !!edit),
    });
  }

  protected askRemove(item: RiskComment, opener: HTMLButtonElement): void {
    if (!this.pending() && this.canEdit(item)) {
      this.removal.set(item);
      this.dialog().show(opener);
    }
  }

  protected remove(): void {
    const item = this.removal();
    if (!item || this.pending() || !this.canEdit(item)) {
      return;
    }
    this.pending.set(true);
    this.error.set('');
    this.api.deleteComment(this.riskId(), item, this.subject()).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.pending.set(false)),
    ).subscribe({
      next: () => {
        if (this.edit()?.id === item.id) {
          this.cancelEdit();
        }
        const state = this.result();
        if (state.kind === 'ready' && state.value.length === 1 && this.offset() > 0) {
          this.page(-1);
        } else {
          this.refresh();
        }
      },
      error: (error: unknown) => this.reportError(error, this.edit()?.id === item.id),
    });
  }

  private reportError(error: unknown, editing: boolean): void {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 409 || error.status === 404) {
        this.conflict.set(editing);
        this.error.set(text.commentConflict);
        this.refresh();
        return;
      }
      if (error.status === 0 || error.status >= 500) {
        this.error.set(text.unknownWrite);
        return;
      }
    }
    this.error.set(errorMessage(error));
  }
}

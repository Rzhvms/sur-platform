import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, Subject, catchError, distinctUntilChanged, map, of, startWith, switchMap, tap } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { errorMessage } from '../../core/http/api';
import { calendarDate, money, utcTime } from '../../shared/format';
import { text } from '../../shared/text';
import { DataState, DataStateView } from '../../shared/ui/data-state';
import { Rating, Status } from '../../shared/ui/status';
import { UiTiming } from '../../core/release/ui-timing';
import { Session } from '../../core/auth/session';
import { RiskComments } from './comments';
import { RiskAssessment } from './assessment';
import { RiskDecisions } from './decisions';
import { RiskTreatments } from '../treatments/risk-treatments';
import { RiskRelated } from './related';
import { exampleRisks } from './demo';
import { RiskDetails, readQuery, urlQuery } from './model';
import { RisksApi } from './risks-api';

const loading: DataState<RiskDetails> = { kind: 'loading' };

@Component({
  selector: 'sur-risk-detail',
  imports: [RouterLink, ReactiveFormsModule, DataStateView, Rating, Status, RiskComments, RiskRelated, RiskTreatments, RiskAssessment, RiskDecisions],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './detail.html',
})
export class RiskDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly session = inject(Session);
  private readonly risks = inject(RisksApi);
  private readonly timing = inject(UiTiming);
  private readonly reload = new Subject<void>();
  private readonly drafts = new Map<string, string>();
  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });
  private readonly demo = this.route.parent?.snapshot.data['demo'] === true;
  /** Выбранный раздел следует за URL, включая переходы назад и вперёд */
  protected readonly section = toSignal(this.route.fragment, { initialValue: this.route.snapshot.fragment });
  protected readonly t = text;
  protected readonly note = new FormControl('', { nonNullable: true });
  protected readonly showId = this.demo;
  protected readonly canEdit = computed(() => !this.demo && this.session.hasRight('edit_risk'));
  protected readonly editQuery = computed(() => ({ ...this.backQuery(), from: this.query().get('from') }));
  protected readonly notFound = signal(false);
  protected readonly money = money;
  protected readonly date = calendarDate;
  protected readonly timestamp = utcTime;
  protected readonly back = computed(() => {
    const page = this.demo ? '/demo/risks' : '/risks';
    return this.query().get('from') === 'board' ? `${page}/board` : page;
  });
  protected readonly backQuery = computed(() => urlQuery(readQuery(this.query())));
  protected readonly state = toSignal(this.route.paramMap.pipe(
    map(params => params.get('id') ?? ''),
    distinctUntilChanged(),
    tap(id => this.note.setValue(this.drafts.get(id) ?? '')),
    switchMap(id => this.reload.pipe(startWith(undefined), switchMap(() => this.fetch(id)), startWith(loading))),
  ), { initialValue: loading });

  protected saveDraft(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.drafts.set(id, this.note.value);
    }
  }

  protected lockReason(risk: RiskDetails): string {
    if (!this.canEdit()) {
      return text.passportNoRight;
    }
    const owner = risk.processes.find(process => process.isPrimary);
    if (!this.session.rights().includes('admin') && (!owner?.ownerActive || owner.ownerUserId !== this.session.userId())) {
      return text.passportNotOwner;
    }
    return text.passportAssessed;
  }

  protected retry(): void {
    this.reload.next();
  }

  private fetch(id: string): Observable<DataState<RiskDetails>> {
    this.notFound.set(false);
    if (this.demo) {
      const item = exampleRisks.find(risk => risk.id === id);
      return of(item ? { kind: 'ready', value: item } : { kind: 'error', message: text.missing });
    }
    return this.risks.details(id).pipe(
      map((value): DataState<RiskDetails> => {
        this.timing.ready('risk-card');
        return { kind: 'ready', value };
      }),
      catchError((error: unknown): Observable<DataState<RiskDetails>> => {
        this.notFound.set(error instanceof HttpErrorResponse && error.status === 404);
        return of({ kind: 'error', message: errorMessage(error) });
      }),
    );
  }
}

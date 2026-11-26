import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, expand, reduce } from 'rxjs';
import { api, errorMessage, problemCode, providerApi } from '../../core/http/api';
import { text } from '../../shared/text';
import {
  Approval, Decision, Executor, JiraDelivery, Transition, Treatment, TreatmentCard, TreatmentDraft, TreatmentQueue, TreatmentRegistry, treatmentPageSize, treatmentText,
} from './treatment-contract';

const riskEndpoint = api + '/risks';
const endpoint = api + '/treatments';
const queueEndpoint = api + '/treatment-approvals';

@Injectable({ providedIn: 'root' })
export class TreatmentsApi {
  private readonly http = inject(HttpClient);

  /** Получает одну страницу реестра одним запросом, без обхода карточек рисков */
  registry(offset: number): Observable<TreatmentRegistry> {
    return this.http.get<TreatmentRegistry>(endpoint, { params: { offset, limit: treatmentPageSize } });
  }

  list(riskId: string, offset: number): Observable<Treatment[]> {
    return this.http.get<Treatment[]>(this.riskUrl(riskId), { params: { offset, limit: treatmentPageSize } });
  }

  create(riskId: string, riskProcessId: string, draft: TreatmentDraft): Observable<Treatment> {
    return this.http.post<Treatment>(this.riskUrl(riskId), { ...draft, riskProcessId });
  }

  start(riskId: string, version: string): Observable<unknown> {
    return this.http.post(this.riskUrl(riskId) + '/start', { version });
  }

  card(id: string): Observable<TreatmentCard> {
    return this.http.get<TreatmentCard>(this.url(id));
  }

  edit(id: string, version: string, draft: TreatmentDraft, riskProcessId: string): Observable<Treatment> {
    return this.http.put<Treatment>(this.url(id), { ...draft, version, riskProcessId });
  }

  history(id: string, offset: number): Observable<Approval[]> {
    return this.http.get<Approval[]>(this.url(id) + '/approvals', { params: { offset, limit: treatmentPageSize } });
  }

  queue(offset: number): Observable<TreatmentQueue> {
    return this.http.get<TreatmentQueue>(queueEndpoint, { params: { offset, limit: treatmentPageSize } });
  }

  submit(id: string, version: string): Observable<Approval> {
    return this.http.post<Approval>(this.url(id) + '/approvals', { version });
  }

  decide(approval: Approval, decision: Decision, reason?: string, confirmed?: boolean): Observable<Approval> {
    return this.http.post<Approval>(queueEndpoint + '/' + encodeURIComponent(approval.id) + '/' + decision,
      { version: approval.version, reason, confirmed });
  }

  /** Список может занимать несколько страниц. Запросы прекращаются при уходе с карточки. */
  executors(id: string): Observable<Executor[]> {
    const read = (offset: number) => this.http.get<Executor[]>(this.url(id) + '/executors', {
      params: { offset, limit: treatmentPageSize },
    });
    return read(0).pipe(
      expand((items, page) => items.length === treatmentPageSize ? read((page + 1) * treatmentPageSize) : EMPTY),
      reduce((all, items) => all.concat(items), [] as Executor[]),
    );
  }

  dispatch(id: string, version: string, assigneeId: string): Observable<Treatment> {
    return this.http.post<Treatment>(this.url(id) + '/dispatch', { version, assigneeId });
  }

  assign(id: string, version: string, assigneeId: string | null): Observable<unknown> {
    return this.http.put(this.url(id) + '/assignment', { version, assigneeId });
  }

  transition(id: string, version: string, action: Transition, costActual: number | null, confirmed?: boolean): Observable<Treatment> {
    return this.http.post<Treatment>(this.url(id) + '/' + action, { version, costActual, confirmed });
  }

  resolve(id: string, attemptStartedAt: string, issue: string | null, confirmAbsent: boolean): Observable<JiraDelivery> {
    return this.http.post<JiraDelivery>(providerApi + '/jira/treatments/' + encodeURIComponent(id) + '/resolve',
      { attemptStartedAt, issue, confirmAbsent });
  }

  private url(id: string): string {
    return endpoint + '/' + encodeURIComponent(id);
  }

  private riskUrl(id: string): string {
    return riskEndpoint + '/' + encodeURIComponent(id) + '/treatments';
  }
}

/** Не выводим произвольное описание внешней ошибки и не предлагаем слепо повторить запись. */
export function treatmentWriteError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    switch (problemCode(error)) {
      case 'TREATMENT_STATE_CONFLICT': return treatmentText.stateConflict;
      case 'TREATMENT_SLOT_TAKEN': return treatmentText.slotTaken;
      case 'TREATMENT_INVALID_EXECUTOR': return treatmentText.invalidExecutor;
      case 'TREATMENT_NO_APPROVER': return treatmentText.noApprover;
      case 'JIRA_MANUAL_CONTROL_BLOCKED': return treatmentText.external;
    }
    if (error.status === 0 || error.status >= 500) {
      return text.unknownWrite;
    }
  }
  return errorMessage(error);
}

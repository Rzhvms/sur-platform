import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Provider, ProviderToken } from '@angular/core';
import { TestBed, TestComponentRenderer } from '@angular/core/testing';
import assert from 'node:assert/strict';
import { Subject, firstValueFrom, switchMap } from 'rxjs';
import { afterEach, onTestFinished, test } from 'vitest';
import { DocumentsApiService } from '../../features/documents/documents-api.service';
import { RisksApiService } from '../../features/risks/risks-api.service';
import { TreatmentsApiService } from '../../features/treatments/treatments-api.service';
import { text } from '../../shared/text';
import { accessToken } from '../auth/access';
import { SessionService } from '../auth/session.service';
import { errorMessage } from './api';

let configured = false;
function setup(providers: Provider[] = []) {
  configured = true;
  TestBed.configureTestingModule({ providers: [
    { provide: DOCUMENT, useValue: { cookie: '', location: { href: 'https://alpha.sur.test/' } } },
    { provide: TestComponentRenderer, useValue: { removeAllRootElements() {} } },
    provideHttpClient(withInterceptors([accessToken])), provideHttpClientTesting(),
    ...providers,
  ] });
  return {
    injector: { get: <T>(token: ProviderToken<T>): T => TestBed.inject(token) },
    http: TestBed.inject(HttpClient),
    requests: TestBed.inject(HttpTestingController),
  };
}
afterEach(() => {
  if (configured) { TestBed.inject(HttpTestingController).verify(); TestBed.resetTestingModule(); }
  configured = false;
});
const anonymous = { token: () => null, sid: () => null };

test('risk adapter maps actual DTOs including a zero assessment and recipient-specific Jira notice', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, RisksApiService]);
  const result = firstValueFrom(injector.get(RisksApiService).details('risk-identity'));
  requests.expectOne('/api/v1/public/risks/risk-identity').flush({
    data: { risk: { id: 'risk-identity', name: 'Сбой канала', status: 'ACTIVE', version: 'risk-version',
      createdAt: '2026-11-26T00:00:00Z', updatedAt: '2026-11-26T00:00:00Z' },
      processes: [], currentAssessment: { riskScore: 0, lossAmount: 0 }, categoryName: 'Операционный риск' },
    previousAssessment: null, passportEditable: false, jiraRetryPending: true, reviews: [],
  });
  const card = await result;
  assert.equal(card.currentAssessment!.riskScore, 0);
  assert.equal(card.process, null);
  assert.equal(card.jiraRetryPending, true);
});

test('switching the search cancels the previous HTTP request', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, RisksApiService]);
  const search = new Subject<string>();
  const values: unknown[] = [];
  const subscription = search.pipe(switchMap(value => injector.get(RisksApiService).list({
    search: value, page: 1, sort: 'name', status: '',
  }))).subscribe(value => values.push(value));
  search.next('старый поиск');
  const old = requests.expectOne(request => request.params.get('search') === 'старый поиск');
  search.next('новый поиск');
  assert.equal(old.cancelled, true);
  requests.expectOne(request => request.params.get('search') === 'новый поиск').flush([]);
  assert.deepEqual(values, [[]]);
  subscription.unsubscribe();
});

test('401 retries a safe read once after renewal and never replays a mutation', async () => {
  let token = 'old-token';
  const session = {
    token: () => token, sid: () => 'same-session',
    restore: () => { token = 'fresh-token'; return new Subject<void>(); },
    notifyRestoreFailure: () => {},
  };
  const refreshed = new Subject<void>();
  session.restore = () => { token = 'fresh-token'; return refreshed; };
  const { http, requests } = setup([{ provide: SessionService, useValue: session }]);
  const read = firstValueFrom(http.get('/api/v1/public/risks'));
  requests.expectOne('/api/v1/public/risks').flush({}, { status: 401, statusText: 'Unauthorized' });
  refreshed.next();
  const retry = requests.expectOne('/api/v1/public/risks');
  assert.equal(retry.request.headers.get('Authorization'), 'Bearer fresh-token');
  retry.flush([]);
  assert.deepEqual(await read, []);

  token = 'old-token';
  const write = firstValueFrom(http.post('/api/v1/public/risks', {}));
  const denied = assert.rejects(write, (error: unknown) => error instanceof HttpErrorResponse && error.status === 401);
  requests.expectOne('/api/v1/public/risks').flush({}, { status: 401, statusText: 'Unauthorized' });
  refreshed.next();
  await denied;
  requests.expectNone('/api/v1/public/risks');
});

test('errors use stable ProblemDetails codes without exposing server prose', () => {
  assert.equal(errorMessage(new HttpErrorResponse({ status: 409, error: { code: 'STALE_VERSION', detail: 'secret fixture' } })), text.stale);
  assert.equal(errorMessage(new HttpErrorResponse({ status: 503, error: { detail: 'secret fixture' } })), text.unavailable);
});

test('real session serializes concurrent refresh behind fresh CSRF and then reads effective rights', async () => {
  const windowBefore = globalThis.window;
  const storageBefore = globalThis.localStorage;
  const locksBefore = Object.getOwnPropertyDescriptor(navigator, 'locks');
  globalThis.window = new EventTarget() as unknown as Window & typeof globalThis;
  const memory = new Map<string, string>();
  Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: {
    getItem: (key: string) => memory.get(key) ?? null,
    setItem: (key: string, value: string) => { memory.set(key, value); },
    removeItem: (key: string) => { memory.delete(key); },
  } });
  Object.defineProperty(navigator, 'locks', { configurable: true, value: { request: async (_name: string, action: () => Promise<unknown>) => action() } });
  onTestFinished(() => {
    globalThis.window = windowBefore;
    Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: storageBefore });
    if (locksBefore) { Object.defineProperty(navigator, 'locks', locksBefore); }
    else { delete (navigator as { locks?: unknown }).locks; }
  });
  const { injector, requests } = setup([SessionService]);
  const session = injector.get(SessionService);
  const one = firstValueFrom(session.restore());
  const two = firstValueFrom(session.restore());
  const tick = () => new Promise<void>(resolve => setTimeout(resolve, 0));
  await tick();
  requests.expectOne('/api/v1/public/connect/browser/csrf').flush({ requestToken: 'fixture-csrf' });
  await tick();
  const refresh = requests.expectOne('/api/v1/public/connect/browser/token/refresh');
  assert.equal(refresh.request.headers.get('X-CSRF-TOKEN'), 'fixture-csrf');
  const payload = btoa(JSON.stringify({ sid: 'session-device-one' })).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const token = 'fixture.' + payload + '.signature';
  refresh.flush({ accessToken: token });
  await tick();
  const profile = requests.expectOne('/api/v1/public/users/me');
  assert.equal(profile.request.headers.get('Authorization'), 'Bearer ' + token);
  profile.flush({ userId: 'risk-owner', rightCodes: ['view_risk'] });
  await Promise.all([one, two]);
  assert.deepEqual(session.rights(), ['view_risk']);
  assert.equal(session.sid(), 'session-device-one');
});

test('binary document errors retain the ProblemDetails code and HTTP status', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, DocumentsApiService]);
  const file = firstValueFrom(injector.get(DocumentsApiService).source('source-id'));
  const rejected = assert.rejects(file, (error: unknown) => error instanceof HttpErrorResponse && error.status === 403 && error.error.code === 'ACCESS_DENIED');
  requests.expectOne('/api/v1/public/documents/source-id/file').flush(
    new Blob([JSON.stringify({ code: 'ACCESS_DENIED', status: 403 })], { type: 'application/problem+json' }),
    { status: 403, statusText: 'Forbidden' },
  );
  await rejected;
});

test('CSV upload sends the original File and leaves length calculation to the browser', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, DocumentsApiService]);
  const csv = new File(['Name,Description\nРиск,Описание'], 'fixture.csv');
  const result = firstValueFrom(injector.get(DocumentsApiService).upload('import-id', csv));
  const upload = requests.expectOne('/api/v1/public/documents/import-id/file');
  assert.equal(upload.request.body, csv);
  assert.equal(upload.request.headers.get('Content-Type'), 'application/octet-stream');
  assert.equal(upload.request.headers.has('Content-Length'), false);
  upload.flush({ id: 'import-id', state: 'AWAITING_MAPPING' });
  assert.equal((await result).state, 'AWAITING_MAPPING');
});

test('dispatch sends the selected employee and observed treatment version in one command', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, TreatmentsApiService]);
  const treatmentId = '962debda-d0d8-4dc4-9d5c-e139d1a29fc0';
  const version = 'approved-treatment-version';
  const employeeId = 'payments-employee-id';
  const response = firstValueFrom(injector.get(TreatmentsApiService).dispatch(treatmentId, version, employeeId));
  const command = requests.expectOne('/api/v1/public/treatments/' + treatmentId + '/dispatch');
  assert.equal(command.request.method, 'POST');
  assert.deepEqual(command.request.body, { version, assigneeId: employeeId });
  command.flush({ id: treatmentId, status: 'WAITING' });
  assert.equal((await response).status, 'WAITING');
});

test('treatment registry reads one bounded page and preserves server pagination and related names', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, TreatmentsApiService]);
  const response = firstValueFrom(injector.get(TreatmentsApiService).registry(25));
  const read = requests.expectOne(request => request.url === '/api/v1/public/treatments');
  assert.equal(read.request.method, 'GET');
  assert.equal(read.request.params.get('offset'), '25');
  assert.equal(read.request.params.get('limit'), '25');
  const page = { items: [{ id: 'payments-control', costPlanned: 0, costActual: null }], hasMore: true,
    riskNames: { 'payment-risk': 'Задержка платежей' }, processNames: { 'payment-process': 'Межбанковские платежи' } };
  read.flush(page);
  assert.deepEqual(await response, page);
});

test('an empty treatment registry is successful data rather than a loading error', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, TreatmentsApiService]);
  const response = firstValueFrom(injector.get(TreatmentsApiService).registry(0));
  const empty = { items: [], hasMore: false, riskNames: {}, processNames: {} };
  requests.expectOne(request => request.url === '/api/v1/public/treatments').flush(empty);
  assert.deepEqual(await response, empty);
});

test('a missing registry route remains an error instead of pretending there are no treatments', async () => {
  const { injector, requests } = setup([{ provide: SessionService, useValue: anonymous }, TreatmentsApiService]);
  const response = firstValueFrom(injector.get(TreatmentsApiService).registry(0));
  const rejected = assert.rejects(response, (error: unknown) => error instanceof HttpErrorResponse && error.status === 404);
  requests.expectOne(request => request.url === '/api/v1/public/treatments').flush(null, { status: 404, statusText: 'Not Found' });
  await rejected;
});

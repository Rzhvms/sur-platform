import '@angular/compiler';
import assert from 'node:assert/strict';
import { afterEach, test } from 'node:test';
import { DOCUMENT } from '@angular/common';
import { TestBed, TestComponentRenderer } from '@angular/core/testing';
import { BrowserTestingModule, platformBrowserTesting } from '@angular/platform-browser/testing';
import { HttpClient, provideHttpClient, withInterceptors, HttpErrorResponse } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { firstValueFrom, Subject, switchMap } from 'rxjs';
import { Session } from '../src/app/core/auth/session.ts';
import { accessToken } from '../src/app/core/auth/access.ts';
import { RisksApi } from '../src/app/features/risks/risks-api.ts';
import { TreatmentsApi } from '../src/app/features/treatments/treatments-api.ts';
import { errorMessage } from '../src/app/core/http/api.ts';
import { text } from '../src/app/shared/text.ts';

TestBed.initTestEnvironment(BrowserTestingModule, platformBrowserTesting());
let configured = false;
function setup(providers = []) {
  configured = true;
  TestBed.configureTestingModule({ providers: [
    { provide: DOCUMENT, useValue: { cookie: '', location: { href: 'https://alpha.sur.test/' } } },
    { provide: TestComponentRenderer, useValue: { removeAllRootElements() {} } },
    provideHttpClient(withInterceptors([accessToken])), provideHttpClientTesting(),
    ...providers,
  ] });
  return { injector: { get: token => TestBed.inject(token) }, http: TestBed.inject(HttpClient), requests: TestBed.inject(HttpTestingController) };
}
afterEach(() => {
  if (configured) { TestBed.inject(HttpTestingController).verify(); TestBed.resetTestingModule(); }
  configured = false;
});
const anonymous = { token: () => null, sid: () => null };
test('risk adapter maps actual DTOs including a zero assessment and recipient-specific Jira notice', async () => {
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, RisksApi]);
  const result = firstValueFrom(injector.get(RisksApi).details('risk-identity'));
  requests.expectOne('/api/v1/public/risks/risk-identity').flush({
    data: { risk: { id: 'risk-identity', name: 'Сбой канала', status: 'ACTIVE', version: 'risk-version',
      createdAt: '2026-11-26T00:00:00Z', updatedAt: '2026-11-26T00:00:00Z' },
      processes: [], currentAssessment: { riskScore: 0, lossAmount: 0 }, categoryName: 'Операционный риск' },
    previousAssessment: null, passportEditable: false, jiraRetryPending: true, reviews: [],
  });
  const card = await result;
  assert.equal(card.currentAssessment.riskScore, 0);
  assert.equal(card.process, null);
  assert.equal(card.jiraRetryPending, true);
});

test('switching the search cancels the previous HTTP request', async () => {
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, RisksApi]);
  const search = new Subject();
  const values = [];
  const subscription = search.pipe(switchMap(value => injector.get(RisksApi).list({
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
    restore: () => { token = 'fresh-token'; return new Subject(); },
    notifyRestoreFailure: () => {},
  };
  const refreshed = new Subject();
  session.restore = () => { token = 'fresh-token'; return refreshed; };
  const { http, requests } = setup([{ provide: Session, useValue: session }]);
  const read = firstValueFrom(http.get('/api/v1/public/risks'));
  requests.expectOne('/api/v1/public/risks').flush({}, { status: 401, statusText: 'Unauthorized' });
  refreshed.next();
  const retry = requests.expectOne('/api/v1/public/risks');
  assert.equal(retry.request.headers.get('Authorization'), 'Bearer fresh-token');
  retry.flush([]);
  assert.deepEqual(await read, []);

  token = 'old-token';
  const write = firstValueFrom(http.post('/api/v1/public/risks', {}));
  const denied = assert.rejects(write, error => error.status === 401);
  requests.expectOne('/api/v1/public/risks').flush({}, { status: 401, statusText: 'Unauthorized' });
  refreshed.next();
  await denied;
  requests.expectNone('/api/v1/public/risks');
});

test('errors use stable ProblemDetails codes without exposing server prose', () => {
  assert.equal(errorMessage(new HttpErrorResponse({ status: 409, error: { code: 'STALE_VERSION', detail: 'secret fixture' } })), text.stale);
  assert.equal(errorMessage(new HttpErrorResponse({ status: 503, error: { detail: 'secret fixture' } })), text.unavailable);
});

test('real session serializes concurrent refresh behind fresh CSRF and then reads effective rights', async t => {
  const windowBefore = globalThis.window;
  const storageBefore = globalThis.localStorage;
  const locksBefore = Object.getOwnPropertyDescriptor(navigator, 'locks');
  globalThis.window = new EventTarget();
  const memory = new Map();
  globalThis.localStorage = { getItem: key => memory.get(key) ?? null, setItem: (key, value) => memory.set(key, value), removeItem: key => memory.delete(key) };
  Object.defineProperty(navigator, 'locks', { configurable: true, value: { request: async (_name, action) => action() } });
  t.after(() => {
    globalThis.window = windowBefore;
    globalThis.localStorage = storageBefore;
    if (locksBefore) { Object.defineProperty(navigator, 'locks', locksBefore); }
    else { delete navigator.locks; }
  });
  const { injector, requests } = setup([Session]);
  const session = injector.get(Session);
  const one = firstValueFrom(session.restore());
  const two = firstValueFrom(session.restore());
  const tick = () => new Promise(resolve => setImmediate(resolve));
  await tick();
  requests.expectOne('/api/v1/public/connect/browser/csrf').flush({ requestToken: 'fixture-csrf' });
  await tick();
  const refresh = requests.expectOne('/api/v1/public/connect/browser/token/refresh');
  assert.equal(refresh.request.headers.get('X-CSRF-TOKEN'), 'fixture-csrf');
  const payload = Buffer.from(JSON.stringify({ sid: 'session-device-one' })).toString('base64url');
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

test('release verification rejects incompatible API data before displaying forms', async () => {
  const { Release } = await import('../src/app/core/release/release.ts');
  const { injector, requests } = setup([Release]);
  const release = injector.get(Release);
  release.check();
  requests.expectOne('/health/release').flush({ version: '0.1.0', revision: 'fixture-revision', contractVersion: 2 });
  assert.equal(release.ready(), false);
  assert.ok(release.message());
  release.check();
  requests.expectOne('/health/release').flush({ version: '0.1.0', revision: 'fixture-revision', contractVersion: 1 });
  assert.equal(release.ready(), true);
});

test('binary document errors retain the ProblemDetails code and HTTP status', async () => {
  const { DocumentsApi } = await import('../src/app/features/documents/documents-api.ts');
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, DocumentsApi]);
  const file = firstValueFrom(injector.get(DocumentsApi).source('source-id'));
  const rejected = assert.rejects(file, error => error.status === 403 && error.error.code === 'ACCESS_DENIED');
  requests.expectOne('/api/v1/public/documents/source-id/file').flush(
    new Blob([JSON.stringify({ code: 'ACCESS_DENIED', status: 403 })], { type: 'application/problem+json' }),
    { status: 403, statusText: 'Forbidden' },
  );
  await rejected;
});

test('CSV upload sends the original File and leaves length calculation to the browser', async () => {
  const { DocumentsApi } = await import('../src/app/features/documents/documents-api.ts');
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, DocumentsApi]);
  const csv = new File(['Name,Description\nРиск,Описание'], 'fixture.csv');
  const result = firstValueFrom(injector.get(DocumentsApi).upload('import-id', csv));
  const upload = requests.expectOne('/api/v1/public/documents/import-id/file');
  assert.equal(upload.request.body, csv);
  assert.equal(upload.request.headers.get('Content-Type'), 'application/octet-stream');
  assert.equal(upload.request.headers.has('Content-Length'), false);
  upload.flush({ id: 'import-id', state: 'AWAITING_MAPPING' });
  assert.equal((await result).state, 'AWAITING_MAPPING');
});

test('dispatch sends the selected employee and observed treatment version in one command', async () => {
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, TreatmentsApi]);
  const treatmentId = '962debda-d0d8-4dc4-9d5c-e139d1a29fc0';
  const version = 'approved-treatment-version';
  const employeeId = 'payments-employee-id';
  const response = firstValueFrom(injector.get(TreatmentsApi).dispatch(treatmentId, version, employeeId));
  const command = requests.expectOne('/api/v1/public/treatments/' + treatmentId + '/dispatch');
  assert.equal(command.request.method, 'POST');
  assert.deepEqual(command.request.body, { version, assigneeId: employeeId });
  command.flush({ id: treatmentId, status: 'WAITING' });
  assert.equal((await response).status, 'WAITING');
});

test('treatment registry reads one bounded page and preserves server pagination and related names', async () => {
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, TreatmentsApi]);
  const response = firstValueFrom(injector.get(TreatmentsApi).registry(25));
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
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, TreatmentsApi]);
  const response = firstValueFrom(injector.get(TreatmentsApi).registry(0));
  const empty = { items: [], hasMore: false, riskNames: {}, processNames: {} };
  requests.expectOne(request => request.url === '/api/v1/public/treatments').flush(empty);
  assert.deepEqual(await response, empty);
});

test('a missing registry route remains an error instead of pretending there are no treatments', async () => {
  const { injector, requests } = setup([{ provide: Session, useValue: anonymous }, TreatmentsApi]);
  const response = firstValueFrom(injector.get(TreatmentsApi).registry(0));
  const rejected = assert.rejects(response, error => error instanceof HttpErrorResponse && error.status === 404);
  requests.expectOne(request => request.url === '/api/v1/public/treatments').flush(null, { status: 404, statusText: 'Not Found' });
  await rejected;
});

import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { provideLocationMocks } from '@angular/common/testing';
import { TestBed, TestComponentRenderer } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, UrlTree, provideRouter } from '@angular/router';
import assert from 'node:assert/strict';
import { firstValueFrom, isObservable, of, throwError } from 'rxjs';
import { afterEach, test } from 'vitest';
import { helpContent } from '../../features/help/help-content';
import { canOpen, homeFor, requireSession, sections } from './access';
import { internalDestination } from './session-policy';
import { SessionService } from './session.service';

// Named principals model effective permissions, never client-side role-name authorization.
const principals: Record<string, string[]> = {
  EMPLOYEE: ['view_risk', 'create_risk', 'comment_risk', 'create_treatment', 'execute_treatment'],
  OWNER: ['view_risk', 'create_risk', 'assess_risk', 'accept_owner_risk', 'approve_owner_treatment', 'view_report', 'export_report'],
  DIRECTOR: ['view_risk', 'accept_director_risk', 'approve_director_treatment', 'view_report', 'export_report'],
  CEO: ['view_risk', 'accept_ceo_risk', 'approve_ceo_treatment', 'view_report', 'export_report'],
  RISK_SPECIALIST: ['view_risk', 'accept_ceo_risk', 'approve_ceo_treatment', 'view_report', 'export_report'],
  ADMIN: ['admin'],
};
const common = ['risks', 'treatments'];
const decisionMaker = [...common, 'approvals', 'reports'];
const expected: Record<string, string[]> = {
  EMPLOYEE: common, OWNER: decisionMaker, DIRECTOR: decisionMaker, CEO: decisionMaker,
  RISK_SPECIALIST: decisionMaker, ADMIN: [...decisionMaker, 'imports', 'admin'],
};
for (const [name, rights] of Object.entries(principals)) {
  test(name + ' sees only sections permitted by effective rights', () => {
    assert.deepEqual(sections.filter(section => canOpen(rights, section)).map(section => section.path), expected[name]);
  });
}
test('multiple roles combine permissions and revoked rights immediately remove sections', () => {
  const combined = [...new Set([...principals['EMPLOYEE'], ...principals['DIRECTOR']])];
  assert.equal(canOpen(combined, sections.find(section => section.path === 'admin')!), false);
  assert.equal(canOpen(combined, sections.find(section => section.path === 'approvals')!), true);
  assert.equal(canOpen(combined.filter(right => !right.startsWith('approve_')), sections.find(section => section.path === 'approvals')!), false);
  assert.equal(homeFor([]), '/forbidden');
  assert.equal(homeFor(['view_report']), '/reports');
});
let configured = false;
afterEach(() => { if (configured) { TestBed.resetTestingModule(); } configured = false; });
async function guard(path: string, session: unknown): Promise<string | boolean> {
  configured = true;
  TestBed.configureTestingModule({ providers: [provideRouter([]), provideLocationMocks(), { provide: DOCUMENT, useValue: { cookie: '', location: { href: 'https://alpha.sur.test/' } } }, { provide: TestComponentRenderer, useValue: { removeAllRootElements() {} } }, { provide: SessionService, useValue: session }] });
  const router = TestBed.inject(Router);
  const result = TestBed.runInInjectionContext(() => requireSession(
    { routeConfig: { path } } as unknown as ActivatedRouteSnapshot,
    { url: '/' + path } as never,
  ));
  const resolved = isObservable(result) ? await firstValueFrom(result) : await result;
  return typeof resolved === 'boolean' ? resolved : router.serializeUrl(resolved as UrlTree);
}
function session(rights: string[], options: { token?: boolean; restricted?: boolean; denied?: boolean } = {}) {
  return { token: () => options.token === false ? null : 'fixture-access', rights: () => rights,
    restricted: () => !!options.restricted,
    restore: () => options.denied ? throwError(() => new HttpErrorResponse({ status: 401 })) : of(undefined) };
}
test('a business user cannot open administration through a direct URL', async () => {
  assert.equal(await guard('admin', session(principals['OWNER'])), '/forbidden');
});
test('a restricted session is sent to password change before any protected section', async () => {
  assert.equal(await guard('risks', session(principals['ADMIN'], { restricted: true })), '/change-password');
});
test('anonymous deep link returns to login when session restoration fails', async () => {
  assert.equal(await guard('admin', session([], { token: false, denied: true })), '/login?next=%2Fadmin');
});
test('help is available without business rights but still requires a normal session', async () => {
  assert.equal(await guard('help', session([])), true);
  assert.equal(internalDestination('/help#help-roles'), '/help#help-roles');
  assert.equal(internalDestination('/help/../admin'), null);
});
test('help explains six system roles and executor assignment without inventing current user roles', () => {
  assert.equal(helpContent.roles.length, 6);
  assert.deepEqual(new Set(helpContent.roles.map(role => role.code)), new Set(Object.keys(principals)));
  assert.match(helpContent.rolesHint, /не список ваших/);
  assert.match(helpContent.executorHint, /не седьмая/);
  assert.ok(helpContent.glossary.some(entry => entry.term === 'UNKNOWN в Jira'));
});

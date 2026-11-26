import assert from 'node:assert/strict';
import test from 'node:test';
import {
  decisionReason, jiraLink, jiraResolution, validTreatmentDraft, treatmentRegistryOffset,
} from '../src/app/features/treatments/treatment-contract.ts';

const draft = {
  title: 'Выделить резервный канал', description: null, dueDate: '2028-02-29', costPlanned: 0, costActual: null,
};

test('draft preserves zero plan, optional actual amount and a calendar date', () => {
  assert.ok(validTreatmentDraft(draft));
  assert.ok(validTreatmentDraft({ ...draft, costActual: 0 }));
  assert.ok(!validTreatmentDraft({ ...draft, costActual: -0.01 }));
  assert.ok(!validTreatmentDraft({ ...draft, dueDate: '2027-02-29' }));
  assert.ok(!validTreatmentDraft({ ...draft, dueDate: '2028-02-29T00:00:00' }));
  assert.ok(!validTreatmentDraft({ ...draft, title: '   ' }));
});

test('rejection and revocation require a reason, approval can omit it', () => {
  assert.equal(decisionReason('approve', ' '), '');
  assert.equal(decisionReason('reject', '\n\t'), null);
  assert.equal(decisionReason('revoke', ''), null);
  assert.equal(decisionReason('reject', '  Расходы требуют пересмотра  '), 'Расходы требуют пересмотра');
});

test('reconciliation uses only the reviewed UNKNOWN attempt and preserves sub-millisecond precision', () => {
  const attempt = '2026-11-15T16:30:20.123456Z';
  const unknown = { state: 'UNKNOWN', attemptStartedAt: attempt };
  assert.deepEqual(jiraResolution(unknown, attempt, ' OPS-34 ', false),
    { attemptStartedAt: attempt, issue: 'OPS-34', confirmAbsent: false });
  assert.deepEqual(jiraResolution(unknown, attempt, 'OPS-34', true),
    { attemptStartedAt: attempt, issue: null, confirmAbsent: true });
  assert.equal(jiraResolution(unknown, attempt, '   ', false), null);
  assert.equal(jiraResolution(unknown, '2026-11-15T16:30:20.123Z', 'OPS-34', false), null);
  assert.equal(jiraResolution({ ...unknown, state: 'LINKED' }, attempt, 'OPS-34', false), null);
});

test('external Jira links require explicit HTTPS without URL credentials', () => {
  assert.equal(jiraLink('https://jira.example.org/context/browse/OPS-34'), 'https://jira.example.org/context/browse/OPS-34');
  for (const value of [null, '//jira.example.org/browse/OPS-34', 'http://jira.example.org', 'https://pat@jira.example.org',
    'javascript:alert(1)', 'https:///']) {
    assert.equal(jiraLink(value), null);
  }
});

test('card polling cancels hidden requests and resumes immediately without overlapping', async () => {
  const { Observable } = await import('rxjs');
  const { pollWhileVisible } = await import('../src/app/features/treatments/visible-poll.ts');
  const observer = new EventTarget();
  observer.visibilityState = 'hidden';

  // Polling uses RxJS timer, which is backed by setInterval.
  const clock = test.mock.timers;
  clock.enable({ apis: ['setInterval'] });
  let started = 0;
  let stopped = 0;
  const subscription = pollWhileVisible(observer, () => new Observable(() => {
    started++;
    return () => stopped++;
  })).subscribe();
  try {
    clock.tick(25_000);
    assert.equal(started, 0);

    observer.visibilityState = 'visible';
    observer.dispatchEvent(new Event('visibilitychange'));
    clock.tick(0);
    assert.equal(started, 1);
    clock.tick(50_000);
    assert.equal(started, 1);

    observer.visibilityState = 'hidden';
    observer.dispatchEvent(new Event('visibilitychange'));
    assert.equal(stopped, 1);
    clock.tick(25_000);
    assert.equal(started, 1);

    observer.visibilityState = 'visible';
    observer.dispatchEvent(new Event('visibilitychange'));
    clock.tick(0);
    assert.equal(started, 2);
  } finally {
    subscription.unsubscribe();
    clock.reset();
  }
  assert.equal(stopped, 2);
});

test('treatment registry accepts aligned page offsets and rejects malformed or overflowing URLs', () => {
  for (const value of [null, '', '0', '25', '50', '2147483625']) {
    assert.equal(treatmentRegistryOffset(value), Number(value ?? 0));
  }
  for (const value of ['-25', '1', '25.5', 'Infinity', 'нечисло', '2147483650']) {
    assert.equal(treatmentRegistryOffset(value), 0);
  }
});

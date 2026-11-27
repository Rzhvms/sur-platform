import assert from 'node:assert/strict';
import { test } from 'vitest';
import {
  internalDestination, readSessionClaims, UnsupportedSessionBrowser, withSessionLock,
} from './session-policy';

test('accepts internal section deep links and rejects external or ambiguous targets', () => {
  assert.equal(internalDestination('/risks/board?status=ACTIVE#stand'), '/risks/board?status=ACTIVE#stand');
  for (const path of [
    null, 'https://another.sur.test/risks', '//another.sur.test/risks', '/login',
    '/risks/../administration', '/risks/%2e%2e/administration', '/risks/%2fother',
    '/risks//another.sur.test', '/risks/\\another.sur.test', '/documents-archive',
  ]) {
    assert.equal(internalDestination(path), null);
  }
});

test('detects restricted sessions without granting rights from JWT claims', () => {
  const token = (claims: object) => `header.${btoa(JSON.stringify(claims)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')}.signature`;
  for (const value of [true, 'true']) {
    assert.deepEqual(readSessionClaims(token({ sid: 'first-session', password_change_required: value, right: 'admin' })),
      { sid: 'first-session', passwordChangeRequired: true });
  }
  assert.deepEqual(readSessionClaims(token({ sid: 'second-session', password_change_required: 'false' })),
    { sid: 'second-session', passwordChangeRequired: false });
  assert.throws(() => readSessionClaims(token({ right: 'admin' })));
  assert.throws(() => readSessionClaims('invalid'));
});

test('uses one origin lock for CSRF plus cookie rotation and fails closed without it', async () => {
  const trace: string[] = [];
  let tail: Promise<unknown> = Promise.resolve();
  const sameOriginLocks = {
    request(name: string, action: () => Promise<string>) {
      assert.equal(name, 'sur:browser-session');
      const scheduled = tail.then(action);
      tail = scheduled.catch(() => {});
      return scheduled;
    },
  };
  const [first, second] = await Promise.all([
    withSessionLock(sameOriginLocks as unknown as LockManager, async () => {
      trace.push('first csrf');
      await Promise.resolve();
      trace.push('first rotation');
      return 'first';
    }),
    withSessionLock(sameOriginLocks as unknown as LockManager, async () => {
      trace.push('second csrf', 'second rotation');
      return 'second';
    }),
  ]);
  assert.deepEqual(trace, ['first csrf', 'first rotation', 'second csrf', 'second rotation']);
  assert.deepEqual([first, second], ['first', 'second']);
  await assert.rejects(withSessionLock(undefined, () => assert.fail('No unlocked auth request')), UnsupportedSessionBrowser);
});

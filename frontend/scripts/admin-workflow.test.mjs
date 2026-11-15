import assert from 'node:assert/strict';
import test from 'node:test';
import { schedulePayload } from '../src/app/features/administration/review-contract.ts';
import { internalDestination } from '../src/app/core/auth/session-policy.ts';

const calendar = {
  startDate: '2028-02-29', endDate: '2028-03-31', recurrenceType: 'WEEKLY', recurrenceInterval: 2,
  weekdays: [1, 7], endType: 'ON_DATE', occurrenceCount: 12,
};

test('weekly schedule keeps calendar dates without timezone conversion', () => {
  assert.deepEqual(schedulePayload(calendar), {
    startDate: '2028-02-29', endDate: '2028-03-31', recurrenceType: 'WEEKLY', recurrenceInterval: 2,
    weekdays: [1, 7], endType: 'ON_DATE', occurrenceCount: null,
  });
});

test('switching to a one-time schedule clears previously selected repetition fields', () => {
  assert.deepEqual(schedulePayload({ ...calendar, recurrenceType: 'NONE' }), {
    startDate: '2028-02-29', endDate: null, recurrenceType: 'NONE', recurrenceInterval: null,
    weekdays: [], endType: 'NEVER', occurrenceCount: null,
  });
});

test('schedule rejects impossible dates, end before start and invalid ISO weekdays', () => {
  assert.equal(schedulePayload({ ...calendar, startDate: '2027-02-29' }), null);
  assert.equal(schedulePayload({ ...calendar, endDate: '2028-02-28' }), null);
  assert.equal(schedulePayload({ ...calendar, weekdays: [0, 1] }), null);
  assert.equal(schedulePayload({ ...calendar, weekdays: [1, 1] }), null);
  assert.equal(schedulePayload({ ...calendar, recurrenceInterval: 0 }), null);
});

test('the login return path accepts admin deep links and rejects path aliases', () => {
  assert.equal(internalDestination('/admin/reviews'), '/admin/reviews');
  assert.equal(internalDestination('/admin/jira'), '/admin/jira');
  assert.equal(internalDestination('/admin//jira'), null);
  assert.equal(internalDestination('/admin/%2e%2e/jira'), null);
});

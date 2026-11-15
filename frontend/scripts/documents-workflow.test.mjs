import assert from 'node:assert/strict';
import test from 'node:test';
import { describeCsv, mappedColumns, previewCsv } from '../src/app/features/documents/csv-preview.ts';
import { calendarDay, canPoll, operation, safeFilename, snapshotTime } from '../src/app/features/documents/document-contract.ts';
import { internalDestination } from '../src/app/core/auth/session-policy.ts';

test('CSV preview preserves multiline fields, escaped quotes and empty columns', () => {
  const result = previewCsv('\uFEFFname;description;asset\r\n"Сбой; сеть";"первая\r\nстрока ""вторая""";\r\n\r\n', ';');
  assert.deepEqual(result, {
    rows: [['name', 'description', 'asset'], ['Сбой; сеть', 'первая\r\nстрока "вторая"', '']],
    invalid: false,
  });
  assert.deepEqual(previewCsv('name,details\n"unfinished', ','), { rows: [['name', 'details']], invalid: true });
  assert.deepEqual(previewCsv('ok\n"unterminated', ',', 1), { rows: [['ok']], invalid: false });
});

test('mapping requires distinct zero-based columns and permits omitting the asset', () => {
  const fields = { Name: '0', Description: '1', HazardId: '2', CategoryId: '3', ProcessId: '4', AssetId: '' };
  assert.deepEqual(mappedColumns(fields), { Name: 0, Description: 1, HazardId: 2, CategoryId: 3, ProcessId: 4 });
  assert.equal(mappedColumns({ ...fields, AssetId: '4' }), null);
  assert.equal(mappedColumns({ ...fields, ProcessId: '128' }), null);
  assert.equal(mappedColumns({ ...fields, Name: '' }), null);
});

test('source fingerprint covers the exact uploaded bytes, including a BOM', async () => {
  const blob = new Blob([Uint8Array.from([239, 187, 191]), 'abc']);
  const data = await describeCsv(blob);
  assert.equal(data.source, 'abc');
  assert.equal(data.size, 6);
  const withoutBom = await describeCsv(new Blob(['abc']));
  assert.equal(withoutBom.checksum, 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
  assert.notEqual(data.checksum, withoutBom.checksum);
  await assert.rejects(describeCsv(new Blob([Uint8Array.of(0xff)])), TypeError);
});

test('historical snapshot preserves the entered offset and validates real dates', () => {
  assert.equal(snapshotTime('2026-11-15T00:10', '+05:00'), '2026-11-15T00:10:00+05:00');
  assert.equal(snapshotTime('2026-11-15T00:10:13', '-03:30'), '2026-11-15T00:10:13-03:30');
  assert.equal(snapshotTime('2026-11-15T24:10', '+05:00'), null);
  assert.equal(snapshotTime('2026-11-15T00:10', '+14:30'), null);
  assert.equal(calendarDay('2028-02-29'), true);
  assert.equal(calendarDay('2027-02-29'), false);
});

test('an attempted registration holds an independent copy of its parameters', () => {
  const draft = { period: { from: '2026-11-01', to: '2026-11-15' } };
  const request = operation(draft);
  draft.period.to = '2026-11-17';
  assert.equal(request.period.to, '2026-11-15');
  assert.notEqual(request.operationId, operation(draft).operationId);
  assert.ok(Object.isFrozen(request));
});

test('download accepts only an expected extension and a safe basename', () => {
  assert.equal(safeFilename("attachment; filename*=UTF-8''%D0%BE%D1%82%D1%87%D1%91%D1%82.csv", 'csv'), 'отчёт.csv');
  assert.equal(safeFilename('attachment; filename="../../report.csv"', 'csv'), '______report.csv');
  assert.equal(safeFilename('attachment; filename="report.csv.exe"', 'csv'), 'document.csv');
  assert.equal(safeFilename('attachment; filename="CON.json"', 'json'), 'document.json');
  assert.equal(safeFilename("attachment; filename*=UTF-8''%ZZ.xlsx", 'xlsx'), 'document.xlsx');
});

test('only queued and active jobs poll and login can return to document deep links', () => {
  assert.equal(canPoll('PENDING'), true);
  assert.equal(canPoll('PROCESSING'), true);
  for (const state of ['FAILED', 'AWAITING_MAPPING', 'UPLOADING', 'COMPLETED', 'COMPLETED_WITH_ERRORS']) {
    assert.equal(canPoll(state), false);
  }
  assert.equal(internalDestination('/imports?job=fixture'), '/imports?job=fixture');
  assert.equal(internalDestination('/reports?job=fixture'), '/reports?job=fixture');
  assert.equal(internalDestination('/reports/../admin/history'), null);
});

import assert from 'node:assert/strict';
import { test } from 'vitest';
import { hasNextColumnPage, pageSize, validOffset } from './board-page';

test('board navigation uses the returned count and filtered total of each column', () => {
  assert.equal(hasNextColumnPage(0, pageSize, 19), true);
  assert.equal(hasNextColumnPage(pageSize, pageSize, 19), true);
  assert.equal(hasNextColumnPage(pageSize * 2, 3, 19), false);
  assert.equal(hasNextColumnPage(pageSize * 3, 0, 19), false);
  assert.equal(hasNextColumnPage(0, 0, 0), false);
});

test('column offsets align with page size and fit the API range', () => {
  assert.equal(validOffset(0), true);
  assert.equal(validOffset(pageSize * 2), true);
  for (const invalid of [-pageSize, 1, 3.5, Infinity, 2_147_483_648]) {
    assert.equal(validOffset(invalid), false);
  }
  assert.equal(hasNextColumnPage(2_147_483_640, pageSize, 2_147_483_650), false);
});

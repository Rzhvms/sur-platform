import assert from 'node:assert/strict';
import { test } from 'vitest';
import { validTemporaryPassword } from './temporary-password';

test('manual issuance accepts matching passwords at both length boundaries', () => {
  for (const password of ['Доступ123', 'Д'.repeat(256), ' Доступ123 ']) {
    assert.equal(validTemporaryPassword(password, password), true);
  }
});

test('manual issuance rejects empty, whitespace, mismatched and oversized passwords', () => {
  for (const password of ['', ' '.repeat(8), 'Коротко', 'Д'.repeat(257)]) {
    assert.equal(validTemporaryPassword(password, password), false);
  }
  assert.equal(validTemporaryPassword('Новый-доступ-2026!', 'Новый-доступ-2025!'), false);
  assert.equal(validTemporaryPassword(' Доступ123 ', 'Доступ123'), false);
});

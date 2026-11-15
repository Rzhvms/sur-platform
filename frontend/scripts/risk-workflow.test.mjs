import assert from 'node:assert/strict';
import test from 'node:test';
import { assessmentInputs, assessmentSubmission, validSplit } from '../src/app/features/risks/assessment-contract.ts';

const fields = {
  likelihoodLevelId: 'likelihood-zero', lossAmount: null, downtimeWeeks: null, downtimeDays: null, downtimeHours: null,
};

test('zero damage and zero downtime are valid and send mutually exclusive inputs', () => {
  assert.deepEqual(assessmentInputs({ ...fields, lossAmount: 0, downtimeDays: 1 }, false),
    { likelihoodLevelId: fields.likelihoodLevelId, lossAmount: 0 });
  assert.deepEqual(assessmentInputs({ ...fields, lossAmount: 875, downtimeHours: 0 }, true),
    { likelihoodLevelId: fields.likelihoodLevelId, downtimeWeeks: null, downtimeDays: null, downtimeHours: 0 });
});

test('empty, negative or invalid values do not produce a preview request', () => {
  assert.equal(assessmentInputs(fields, false), null);
  assert.equal(assessmentInputs(fields, true), null);
  assert.equal(assessmentInputs({ ...fields, lossAmount: -1 }, false), null);
  assert.equal(assessmentInputs({ ...fields, lossAmount: 12.345 }, false), null);
  assert.equal(assessmentInputs({ ...fields, downtimeDays: 1.5 }, true), null);
  assert.equal(assessmentInputs({ ...fields, downtimeWeeks: 2_147_483_648 }, true), null);
});

test('confirmation sends exactly the inputs and versions from the preview', () => {
  const inputs = { likelihoodLevelId: fields.likelihoodLevelId, lossAmount: 0 };
  const preview = { riskVersion: 'risk-v2', scaleVersionId: 'scale-v5', assetVersion: null };
  assert.deepEqual(assessmentSubmission(inputs, preview),
    { version: 'risk-v2', scaleVersionId: 'scale-v5', assetVersion: null, inputs });
  assert.equal(assessmentSubmission(inputs, { ...preview, assetVersion: 'asset-v3' }).assetVersion, 'asset-v3');
});

test('split requires at least two distinct processes and a primary in that set', () => {
  assert.equal(validSplit(['operations', 'delivery'], 'delivery'), true);
  assert.equal(validSplit(['operations', 'delivery'], 'finance'), false);
  assert.equal(validSplit(['operations', 'operations'], 'operations'), false);
  assert.equal(validSplit(['operations'], 'operations'), false);
});

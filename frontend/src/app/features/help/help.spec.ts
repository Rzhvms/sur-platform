import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { test } from 'vitest';
import { helpContent } from './help-content';

test('help exposes unique stable anchors for all required workflows and glossary entries', () => {
  const requiredWorkflows = ['help-login', 'help-register-risk', 'help-assess-risk', 'help-approve-treatment',
    'help-execute-treatment', 'help-review-risk', 'help-reports', 'help-import', 'help-admin'];
  const requiredTerms = ['term-risk', 'term-hazard', 'term-process', 'term-likelihood', 'term-impact',
    'term-assessment', 'term-acceptance', 'term-measure', 'term-review'];
  const workflowIds = helpContent.workflows.map(entry => entry.id);
  const termIds = helpContent.glossary.map(entry => entry.id);
  for (const id of requiredWorkflows) { assert.ok((workflowIds as string[]).includes(id), id); }
  for (const id of requiredTerms) { assert.ok((termIds as string[]).includes(id), id); }
  const ids = [...workflowIds, ...termIds];
  assert.equal(new Set(ids).size, ids.length);
  assert.ok(ids.every(id => /^[a-z][a-z-]+$/.test(id)));
  assert.ok(helpContent.workflows.every(step => step.description && step.rights));
});

test('help links every term to a focusable fragment and identifies sources and scope limits', () => {
  const template = readFileSync('src/app/features/help/help/help.component.html', 'utf8');
  assert.match(template, /\[fragment\]="entry.id"/);
  assert.match(template, /\[id\]="entry.id" tabindex="-1"/);
  assert.match(template, /\[id\]="step.id" tabindex="-1"/);
  assert.ok(helpContent.sources.every(source => new URL(source.href).protocol === 'https:'));
  assert.match(helpContent.gostNote, /заменён/);
  assert.match(helpContent.scopeNote, /PDF/);
  assert.match(helpContent.scopeNote, /отдельным этапом/);
  assert.match(helpContent.standardsNote, /не обязательная формула/);
  assert.match(helpContent.workflows.find(step => step.id === 'help-import')!.rights, /import_data.*create_risk/);
  assert.match(helpContent.workflows.find(step => step.id === 'help-admin')!.rights, /admin/);
});

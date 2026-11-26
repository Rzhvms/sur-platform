import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { parseTemplate } from '@angular/compiler';

for (const scenario of [
  { name: 'approval confirmation', file: 'approval-actions.ts', inline: true },
  { name: 'executor dispatch and treatment transitions', file: 'treatment-detail.html', inline: false },
]) {
 test(`${scenario.name} binds a reactive form so ngSubmit receives the native submit event`, () => {
  const source = readFileSync(new URL('../src/app/features/treatments/' + scenario.file, import.meta.url), 'utf8');
  const template = scenario.inline ? source.match(/template:\s*`([\s\S]*?)`/)?.[1] : source;
  assert.ok(template, 'The command must expose a template');
  const parsed = parseTemplate(template, scenario.file);
  assert.equal(parsed.errors, null);
  const forms = [];
  function visit(nodes) {
    for (const node of nodes ?? []) {
      if (node.name === 'form') { forms.push(node); }
      visit(node.children);
      for (const branch of node.branches ?? []) { visit(branch.children); }
    }
  }
  visit(parsed.nodes);
  const confirmation = forms.find(node => node.outputs.some(output => output.name === 'ngSubmit'));
  assert.ok(confirmation, 'The confirmation must handle ngSubmit');
  assert.ok(confirmation.inputs.some(input => input.name === 'formGroup'),
    'ReactiveFormsModule requires FormGroupDirective on a form emitting ngSubmit');
});
}

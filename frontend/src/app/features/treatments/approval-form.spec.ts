import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { parseTemplate } from '@angular/compiler';
import { test } from 'vitest';

for (const scenario of [
  { name: 'approval confirmation', file: './approval-actions/approval-actions.component.html' },
  { name: 'executor dispatch and treatment transitions', file: './treatment-detail/treatment-detail.component.html' },
]) {
  test(`${scenario.name} binds a reactive form so ngSubmit receives the native submit event`, () => {
    const template = readFileSync(new URL(scenario.file, import.meta.url), 'utf8');
    const parsed = parseTemplate(template, scenario.file);
    assert.equal(parsed.errors, null);
    const forms: Array<{ name: string; outputs: Array<{ name: string }>; inputs: Array<{ name: string }>; children: unknown[]; branches?: Array<{ children: unknown[] }> }> = [];
    function visit(nodes: unknown[]) {
      for (const node of nodes ?? []) {
        const element = node as typeof forms[number];
        if (element.name === 'form') { forms.push(element); }
        visit(element.children);
        for (const branch of element.branches ?? []) { visit(branch.children); }
      }
    }
    visit(parsed.nodes);
    const confirmation = forms.find(node => node.outputs.some(output => output.name === 'ngSubmit'));
    assert.ok(confirmation, 'The confirmation must handle ngSubmit');
    assert.ok(confirmation.inputs.some(input => input.name === 'formGroup'),
      'ReactiveFormsModule requires FormGroupDirective on a form emitting ngSubmit');
  });
}

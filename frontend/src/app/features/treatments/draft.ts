import { ChangeDetectionStrategy, Component, OnInit, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { text } from '../../shared/text';
import { TreatmentDraft, treatmentText, validTreatmentDraft } from './treatment-contract';

@Component({
  selector: 'sur-treatment-draft',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <form class="form-stack" [formGroup]="form" (ngSubmit)="submit()">
      @if (processes().length) {
        <label>{{ t.process }}
          <select formControlName="process">
            @for (process of processes(); track process.id) { <option [value]="process.id">{{ process.name }}</option> }
          </select>
        </label>
      }
      <label>{{ t.title }} <input formControlName="title" required /></label>
      <label>{{ t.description }} <textarea formControlName="description" rows="3"></textarea></label>
      <label>{{ t.due }} <input type="date" formControlName="dueDate" required /></label>
      <label>{{ w.plan }} <input type="number" min="0" step="0.01" formControlName="costPlanned" required /></label>
      <label>{{ w.actual }} <input type="number" min="0" step="0.01" formControlName="costActual" /></label>
      @if (invalid) { <p role="alert">{{ t.invalid }}</p> }
      <button class="primary" type="submit" [disabled]="blocked() || form.invalid">{{ t.save }}</button>
    </form>
  `,
})
export class TreatmentDraftForm implements OnInit {
  readonly initial = input<TreatmentDraft>();
  readonly initialProcessId = input('');
  readonly processes = input<{ id: string; name: string }[]>([]);
  readonly blocked = input(false);
  readonly save = output<{ draft: TreatmentDraft; riskProcessId: string }>();
  protected readonly t = text;
  protected readonly w = treatmentText;
  protected invalid = false;
  protected readonly form = new FormGroup({
    process: new FormControl('', { nonNullable: true }),
    title: new FormControl('', { nonNullable: true, validators: Validators.required }),
    description: new FormControl('', { nonNullable: true }),
    dueDate: new FormControl('', { nonNullable: true, validators: Validators.required }),
    costPlanned: new FormControl<number | null>(null, [Validators.required, Validators.min(0)]),
    costActual: new FormControl<number | null>(null, Validators.min(0)),
  });

  /** Подставляем серверные данные при открытии формы. Фоновый опрос черновик не перезаписывает. */
  ngOnInit(): void {
    const draft = this.initial();
    if (draft) {
      this.form.patchValue({ ...draft, dueDate: draft.dueDate.slice(0, 10), description: draft.description ?? '' });
    }
    this.form.controls.process.setValue(this.initialProcessId() || this.processes()[0]?.id || '');
  }

  protected submit(): void {
    const { process, costPlanned, costActual, dueDate, title, description } = this.form.getRawValue();
    const draft = { title: title.trim(), description: description.trim() || null, dueDate, costPlanned: costPlanned ?? NaN, costActual };
    this.invalid = this.form.invalid || !validTreatmentDraft(draft)
      || (this.processes().length > 0 && !this.processes().some(candidate => candidate.id === process));
    if (!this.invalid && !this.blocked()) {
      this.save.emit({ draft, riskProcessId: process });
    }
  }
}

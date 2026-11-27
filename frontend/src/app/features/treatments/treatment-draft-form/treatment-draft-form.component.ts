import { Component, OnInit, input, output } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { text } from '../../../shared/text';
import { TreatmentDraft, treatmentText, validTreatmentDraft } from '../treatment-contract';

@Component({
  selector: 'sur-treatment-draft',
  imports: [ReactiveFormsModule],
  templateUrl: './treatment-draft-form.component.html',
})
export class TreatmentDraftFormComponent implements OnInit {
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

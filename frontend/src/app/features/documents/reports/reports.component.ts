import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { SessionService } from '../../../core/auth/session.service';
import { statuses, text } from '../../../shared/text';
import { guid } from '../../risks/model';
import { RisksApiService } from '../../risks/risks-api.service';
import { DocumentJobViewComponent } from '../document-job-view/document-job-view.component';
import { DocumentRequest } from '../document-request';
import { ReportFilters, ReportFormat, documentRights, documentText, snapshotTime } from '../document-contract';
import { DocumentsApiService } from '../documents-api.service';

const field = (value = '') => new FormControl(value, { nonNullable: true });

@Component({
  imports: [ReactiveFormsModule, DocumentJobViewComponent],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss',
})
export class ReportsComponent {
  private readonly api = inject(DocumentsApiService);
  private readonly risks = inject(RisksApiService);
  private readonly session = inject(SessionService);
  protected readonly request = new DocumentRequest<ReportFilters>();
  protected readonly w = documentText;
  protected readonly t = text;
  protected readonly statuses = Object.entries(statuses);
  protected readonly processes = toSignal(this.session.hasRight('view_risk')
    ? this.risks.processes().pipe(catchError(() => of([]))) : of([]), { initialValue: [] });
  protected readonly categories = toSignal(this.session.hasRight('view_risk')
    ? this.risks.references('Categories').pipe(catchError(() => of([]))) : of([]), { initialValue: [] });
  protected readonly directorates = computed(() => {
    const seen = new Map<string, string>();
    for (const item of this.processes()) {
      if (item.directorateId && item.directorateName) { seen.set(item.directorateId, item.directorateName); }
    }
    return [...seen].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });
  protected readonly form = new FormGroup({
    historical: new FormControl(false, { nonNullable: true }), local: field(), offset: field('+00:00'),
    processId: field(), directorateId: field(), categoryId: field(), status: field(),
  });

  protected mayExport(): boolean {
    return this.session.hasRight(documentRights.view) && this.session.hasRight(documentRights.export);
  }

  protected submit(format: ReportFormat | null): void {
    const previous = this.request.attempt();
    if (previous) {
      if (!previous.format || this.mayExport()) {
        this.request.send(previous, body => this.api.report(body));
      }
      return;
    }
    const target = format ?? (this.mayExport() ? 'CSV' : null);
    if (target && !this.mayExport()) { return; }
    const form = this.form.getRawValue();
    const asOf = form.historical ? snapshotTime(form.local, form.offset.trim()) : null;
    const processId = form.processId.trim() || null;
    const directorateId = form.directorateId.trim() || null;
    const categoryId = form.categoryId.trim() || null;
    if ((form.historical && (!asOf || Date.parse(asOf) > Date.now())) ||
      [processId, directorateId, categoryId].some(id => id !== null && !guid.test(id))) {
      this.request.error.set(documentText.invalidSnapshot);
      return;
    }
    this.request.send({ asOf, processId, directorateId, categoryId, status: form.status || null, format: target }, body => this.api.report(body));
  }
}

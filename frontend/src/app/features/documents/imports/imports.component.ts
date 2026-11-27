import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { finalize, forkJoin, from, switchMap } from 'rxjs';
import { SessionService } from '../../../core/auth/session.service';
import { text } from '../../../shared/text';
import { RisksApiService } from '../../risks/risks-api.service';
import { csvFields, CsvField, describeCsv, detectColumns, mappedColumns, previewCsv } from '../csv-preview';
import { DocumentJobViewComponent } from '../document-job-view/document-job-view.component';
import { DocumentRequest } from '../document-request';
import { DocumentJob, ImportMapping, ImportSource, documentRights, documentText, maxCsvBytes } from '../document-contract';
import { DocumentsApiService, documentError } from '../documents-api.service';

type Selected = { jobId: string | null; file: File; source: string; size: number; checksum: string };

const templateFilename = 'risk-import-template.csv';
const templateColumns = ['Name', 'Description', 'HazardId', 'CategoryId', 'ProcessId'];
const templateName = 'Пример риска';
const templateDescription = 'Описание примера риска';

@Component({
  imports: [ReactiveFormsModule, DocumentJobViewComponent],
  templateUrl: './imports.component.html',
  styleUrl: './imports.component.scss',
})
export class ImportsComponent {
  private readonly api = inject(DocumentsApiService);
  private readonly risks = inject(RisksApiService);
  private readonly session = inject(SessionService);
  private readonly destroy = inject(DestroyRef);
  private trackedJobId: string | null = null;
  private detectedPreview: ReturnType<typeof previewCsv> | null = null;
  protected readonly request = new DocumentRequest<ImportSource>();
  protected readonly t = text;
  protected readonly w = documentText;
  protected readonly labels = csvFields;
  protected readonly fields = Object.keys(csvFields) as CsvField[];
  protected readonly selected = signal<Selected | null>(null);
  protected readonly remoteSource = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly templateBusy = signal(false);
  protected readonly error = signal('');
  protected readonly job = signal<DocumentJob | null>(null);
  protected readonly revision = signal(0);
  protected readonly frozenMapping = signal<ImportMapping | null>(null);
  protected readonly delimiter = signal<',' | ';'>(',');
  protected readonly header = signal(true);
  protected readonly matching = new FormGroup({
    Name: new FormControl('0', { nonNullable: true }), Description: new FormControl('1', { nonNullable: true }),
    HazardId: new FormControl('2', { nonNullable: true }), CategoryId: new FormControl('3', { nonNullable: true }),
    ProcessId: new FormControl('4', { nonNullable: true }), AssetId: new FormControl('', { nonNullable: true }),
  });
  protected readonly currentJob = computed(() => {
    const job = this.job();
    return job?.id === this.request.id() ? job : null;
  });
  protected readonly selectedFile = computed(() => {
    const file = this.selected();
    return file?.jobId === this.request.id() ? file : null;
  });
  protected readonly preview = computed(() => {
    const job = this.currentJob();
    const file = this.selectedFile();
    const source = job?.state === 'AWAITING_MAPPING'
      ? this.remoteSource() : file?.source;
    return source !== undefined && source !== null ? previewCsv(source, this.delimiter()) : null;
  });
  protected readonly columnOptions = computed(() => {
    const cells = this.preview()?.rows?.[0]?.slice(0, 128) ?? [];
    return cells.map((cell, index) => ({
      value: String(index),
      label: this.header() && cell.trim() ? `${index} · ${cell.slice(0, 80)}` : `${this.w.column} ${index}`,
    }));
  });
  protected readonly mayStart = computed(() => this.session.hasRight(documentRights.create));

  constructor() {
    effect(() => {
      const frame = this.preview();
      if (!frame || frame === this.detectedPreview) { return; }
      this.detectedPreview = frame;
      if (!this.header()) { return; }
      const detected = detectColumns(frame.rows[0] ?? [], frame.rows[0]?.length ?? 0);
      this.matching.patchValue(Object.fromEntries(
        Object.entries(detected).map(([field, index]) => [field, String(index)]),
      ));
    });
  }

  protected choose(input: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file || this.busy() || this.request.busy() || this.request.attempt()) { return; }
    const jobId = this.request.id();
    this.error.set('');
    this.selected.set(null);
    if (!file.size || file.size > maxCsvBytes) {
      this.error.set(documentText.tooLarge);
      input.value = '';
      return;
    }
    this.busy.set(true);
    from(describeCsv(file)).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))).subscribe({
      next: description => this.selected.set({ jobId, file, ...description }),
      error: () => { this.error.set(documentText.utf8); input.value = ''; },
    });
  }

  protected downloadTemplate(): void {
    if (this.templateBusy()) { return; }
    this.error.set('');
    this.templateBusy.set(true);
    forkJoin({
      hazards: this.risks.references('Hazards'), categories: this.risks.references('Categories'), processes: this.risks.processes(),
    }).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.templateBusy.set(false))).subscribe({
      next: ({ hazards, categories, processes }) => {
        const [hazard] = hazards;
        const [category] = categories;
        const [process] = processes;
        if (!hazard || !category || !process) { this.error.set(documentText.templateEmpty); return; }
        const rows = [templateColumns, [templateName, templateDescription, hazard.id, category.id, process.id]];
        const csv = rows.map(row => row.map(cell => `"${cell.replaceAll('"', '""')}"`).join(',')).join('\r\n');
        const url = URL.createObjectURL(new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' }));
        const link = document.createElement('a');
        link.href = url;
        link.download = templateFilename;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: error => this.error.set(documentError(error)),
    });
  }

  protected register(): void {
    const file = this.selectedFile();
    const existing = this.request.attempt();
    const accepted = (job: DocumentJob) => {
      if (file && job.state === 'UPLOADING') { this.selected.set({ ...file, jobId: job.id }); }
    };
    if (existing) {
      this.request.send(existing, body => this.api.register(body), accepted);
    } else if (file) {
      this.request.send({ size: file.size, checksum: file.checksum }, body => this.api.register(body), accepted);
    }
  }

  protected upload(): void {
    const file = this.selectedFile();
    const job = this.currentJob();
    if (!file || job?.state !== 'UPLOADING' || this.busy()) { return; }
    this.busy.set(true);
    this.error.set('');
    this.api.upload(job.id, file.file).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))).subscribe({
      next: status => {
        if (this.request.id() !== status.id) { return; }
        this.statusChanged(status);
        if (status.state === 'AWAITING_MAPPING') { this.remoteSource.set(file.source); }
        this.refresh();
      },
      error: error => { this.error.set(documentError(error, true)); this.refresh(); },
    });
  }

  protected readSource(): void {
    const job = this.currentJob();
    if (!job?.fileId || job.state !== 'AWAITING_MAPPING' || this.busy()) { return; }
    this.busy.set(true);
    this.error.set('');
    this.api.source(job.fileId).pipe(
      switchMap(blob => {
        if (!blob.size || blob.size > maxCsvBytes) { throw new RangeError(); }
        return from(describeCsv(blob));
      }),
      takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false)),
    ).subscribe({
      next: file => { if (this.request.id() === job.id) { this.remoteSource.set(file.source); } },
      error: error => this.error.set(error instanceof TypeError ? documentText.utf8 : error instanceof RangeError ? documentText.tooLarge : documentError(error)),
    });
  }

  protected start(): void {
    const job = this.currentJob();
    if (job?.state !== 'AWAITING_MAPPING' || !this.mayStart() || this.busy()) { return; }
    const columns = mappedColumns(this.matching.getRawValue());
    const mapping = this.frozenMapping() ?? (columns ? {
      delimiter: this.delimiter(), hasHeader: this.header(), columns,
    } : null);
    if (!mapping) { this.error.set(documentText.invalidMapping); return; }
    this.busy.set(true);
    this.error.set('');
    this.frozenMapping.set(mapping);
    this.api.mapping(job.id, mapping).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))).subscribe({
      next: status => { this.statusChanged(status); this.refresh(); },
      error: error => { this.error.set(documentError(error, true)); this.refresh(); },
    });
  }

  protected statusChanged(job: DocumentJob | null): void {
    if (job && job.id !== this.request.id()) { return; }
    if (job && this.trackedJobId !== job.id) {
      this.trackedJobId = job.id;
      this.frozenMapping.set(null);
      this.remoteSource.set(null);
    }
    this.job.set(job);
    if (job && job.state !== 'AWAITING_MAPPING') { this.frozenMapping.set(null); }
  }

  protected reset(): void {
    this.trackedJobId = null;
    this.detectedPreview = null;
    this.request.reset();
    this.selected.set(null);
    this.remoteSource.set(null);
    this.job.set(null);
    this.error.set('');
    this.frozenMapping.set(null);
    this.matching.reset();
    this.delimiter.set(',');
    this.header.set(true);
  }

  private refresh(): void {
    this.revision.update(value => value + 1);
  }
}

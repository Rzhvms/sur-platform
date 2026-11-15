import { DOCUMENT } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, from, map, mergeMap, of, throwError } from 'rxjs';
import { api, errorMessage, problemCode, providerApi } from '../../core/http/api';
import { text } from '../../shared/text';
import { DatePeriod, DocumentJob, DocumentRow, ImportMapping, ImportSource, JsonRecord, Operation, ReportFilters, documentPageSize, documentText, safeFilename } from './document-contract';

const path = api + '/documents';
const historyRoute = providerApi + '/business-history';

@Injectable({ providedIn: 'root' })
export class DocumentsApi {
  private readonly http = inject(HttpClient);
  private readonly document = inject(DOCUMENT);

  report(body: Operation<ReportFilters>): Observable<DocumentJob> {
    return this.http.post<DocumentJob>(path + '/reports', body);
  }

  history(body: Operation<{ period: DatePeriod }>): Observable<DocumentJob> {
    return this.http.post<DocumentJob>(providerApi + '/documents/history', body);
  }

  events(period: DatePeriod, offset: number): Observable<JsonRecord[]> {
    return this.http.get<JsonRecord[]>(historyRoute, { params: { ...period, offset, limit: documentPageSize } });
  }

  register(body: Operation<ImportSource>): Observable<DocumentJob> {
    return this.http.post<DocumentJob>(path + '/imports', body);
  }

  upload(id: string, file: File): Observable<DocumentJob> {
    // File передаёт точный размер в сетевой стек браузера. Content-Length выставляет сам браузер.
    return this.http.put<DocumentJob>(this.job(id) + '/file', file, { headers: { 'Content-Type': 'application/octet-stream' } });
  }

  mapping(id: string, body: ImportMapping): Observable<DocumentJob> {
    return this.http.put<DocumentJob>(this.job(id) + '/mapping', body);
  }

  status(id: string): Observable<DocumentJob> {
    return this.http.get<DocumentJob>(this.job(id));
  }

  rows(id: string, after: number): Observable<DocumentRow[]> {
    return this.http.get<DocumentRow[]>(this.job(id) + '/rows', { params: { after, limit: documentPageSize } });
  }

  retry(id: string): Observable<DocumentJob> {
    return this.http.post<DocumentJob>(this.job(id) + '/retry', null);
  }

  source(id: string): Observable<Blob> {
    return this.http.get(this.job(id) + '/file', { responseType: 'blob' }).pipe(catchError(binaryError));
  }

  download(job: DocumentJob): Observable<void> {
    if (!job.fileId) { return throwError(() => new Error(documentText.noPreview)); }
    return this.http.get(this.job(job.fileId) + '/file', { observe: 'response', responseType: 'blob' }).pipe(
      catchError(binaryError),
      map(response => {
        if (!response.body) { throw new Error(documentText.noPreview); }
        const extension = job.kind === 'HISTORY' ? 'json' :
          response.headers.get('Content-Type')?.includes('spreadsheetml') ? 'xlsx' : 'csv';
        const url = URL.createObjectURL(response.body);
        const link = this.document.createElement('a');
        link.href = url;
        link.download = safeFilename(response.headers.get('Content-Disposition'), extension);
        this.document.body.append(link);
        try { link.click(); } finally {
          link.remove();
          // Браузер успевает принять источник после клика, прежде чем URL освобождается.
          setTimeout(() => URL.revokeObjectURL(url), 1000);
        }
      }),
    );
  }

  private job(id: string): string {
    return path + '/' + encodeURIComponent(id);
  }
}

function binaryError(error: unknown): Observable<never> {
  if (!(error instanceof HttpErrorResponse) || !(error.error instanceof Blob) || error.error.size > 64 * 1024) {
    return throwError(() => error);
  }
  return from(error.error.text()).pipe(
    map(body => JSON.parse(body) as unknown),
    catchError(() => of(null)),
    mergeMap(body => throwError(() => new HttpErrorResponse({
      error: body, status: error.status, statusText: error.statusText, url: error.url ?? undefined, headers: error.headers,
    }))),
  );
}

export function documentError(error: unknown, write = false): string {
  if (error instanceof HttpErrorResponse) {
    switch (problemCode(error)) {
      case 'DOCUMENT_CONFLICT': return documentText.conflict;
      case 'DOCUMENT_NOT_FOUND': return text.missing;
      case 'DOCUMENT_UNAVAILABLE': return text.unavailable;
      case 'IMPORT_INVALID_CSV': return documentText.invalidCsv;
    }
    if (write && (!error.status || error.status >= 500)) { return documentText.unknownCommand; }
  }
  return errorMessage(error);
}

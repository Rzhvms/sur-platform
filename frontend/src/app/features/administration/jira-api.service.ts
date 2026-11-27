import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, expand, reduce } from 'rxjs';
import { providerApi } from '../../core/http/api';

const endpoint = providerApi + '/jira';
export const initialVersion = '00000000-0000-0000-0000-000000000000';
export const jiraLogPageSize = 25;
const userPageSize = 100;

export interface JiraSettings {
  version: string;
  baseUrl: string;
  project: string;
  issueType: string;
  doneStatus: string;
  holdStatus: string | null;
  cancelStatus: string | null;
  enabled: boolean;
  responsibleUserId: string;
  executors: Record<string, string>;
  timeZone: string;
  createdById: string | null;
  updatedById: string;
  pendingCount: number | null;
  unknownCount: number | null;
  restorationRetryPending: boolean;
}
export interface JiraUser {
  userId: string;
  isActive: boolean;
  fullName: string | null;
  email: string | null;
}
interface JiraOption { id: string; name: string }
export interface JiraMetadata {
  projects?: (JiraOption & { key: string })[];
  issueTypes?: (JiraOption & { subtask?: boolean })[];
  statuses?: { id: string; statuses: JiraOption[] }[];
}
export interface JiraLogItem {
  id: string;
  treatmentId: string | null;
  operation: string;
  issueId: string | null;
  issueKey: string | null;
  retryState: string | null;
  code: string;
  occurredAt: string;
}

@Injectable({ providedIn: 'root' })
export class JiraApiService {
  private readonly http = inject(HttpClient);

  settings(): Observable<JiraSettings | null> {
    return this.http.get<JiraSettings | null>(endpoint);
  }

  /** Контракт ответа не содержит PAT и секрета webhook. */
  save(settings: Pick<JiraSettings, 'version' | 'baseUrl' | 'project' | 'issueType' | 'doneStatus' | 'holdStatus' | 'cancelStatus' | 'enabled' | 'responsibleUserId' | 'executors'>, restToken: string, webhookSecret: string): Observable<JiraSettings> {
    return this.http.put<JiraSettings>(endpoint, { ...settings, ...(restToken ? { restToken } : {}), ...(webhookSecret ? { webhookSecret } : {}) });
  }

  discover(baseUrl: string, restToken: string, project: string | null): Observable<JiraMetadata> {
    return this.http.post<JiraMetadata>(endpoint + '/metadata', { baseUrl, restToken: restToken || null, project });
  }

  users(): Observable<JiraUser[]> {
    const read = (after: string) => this.http.get<JiraUser[]>(providerApi + '/users', { params: { after, limit: userPageSize } });
    return read(initialVersion).pipe(
      expand(items => items.length === userPageSize ? read(items[items.length - 1].userId) : EMPTY),
      reduce((all, rows) => all.concat(rows), [] as JiraUser[]),
    );
  }

  errors(offset: number): Observable<JiraLogItem[]> {
    return this.http.get<JiraLogItem[]>(endpoint + '/errors', { params: { offset, limit: jiraLogPageSize } });
  }
}

import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, expand, map, reduce } from 'rxjs';
import { api } from '../../core/http/api';
import type { AssessmentInputs, AssessmentVersions } from './assessment-contract';
import { RiskStatus } from '../../shared/text';
import {
  Assessment, Passport, PassportFields, RiskColumn, RiskComment, RiskDetails, RiskOption, RiskProcess,
  RiskQuery, RiskReference, RiskReview, RiskSummary, apiQuery, pageSize,
} from './model';

const endpoint = `${api}/risks`;
const assessmentSettings = `${api}/risk-assessment-settings`;
const referenceEndpoint = `${api}/risk-references`;
const referencePageSize = 100;
export const historyPageSize = 25;

export interface AssessmentLevel { id: string; weight: number; name: string }
export interface ProcessOption extends RiskOption { directorateId: string; directorateName: string }
export interface AssessmentMatrix { name: string; formula: string; likelihoodLevels: AssessmentLevel[]; impactLevels: AssessmentLevel[]; scores: number[][] }
export interface ImpactScale {
  version: { id: string; sequence: number; createdAt: string };
  thresholds: { weight: number; minLossAmount: number; maxLossAmount: number | null }[];
}
export interface AssessmentPreview extends AssessmentVersions { assessment: Assessment; acceptanceLevel: string }
export type RiskAction = 'accept' | 'treat' | 'split' | 'transfer' | 'close' | 'reopen';
export interface RiskActionBody {
  version: string;
  processIds?: string[];
  primaryProcessId?: string;
  processId?: string;
  reason?: string;
  confirmed?: boolean;
}

interface RiskRowDto {
  risk: PassportFields & { id: string; version: string; status: RiskStatus; updatedAt: string; createdAt: string; acceptanceLevel: string | null; closeReason: string | null };
  processes: RiskProcess[];
  currentAssessment: Assessment | null;
  categoryName: string;
  hazardName: string;
  assetName: string | null;
  authorName: string | null;
}

interface RiskColumnDto { status: RiskStatus; totalCount: number; count: number; items: RiskRowDto[] }
interface RiskCardDto { data: RiskRowDto; previousAssessment: Assessment | null; passportEditable: boolean; jiraRetryPending?: boolean; reviews: RiskReview[] }

@Injectable({ providedIn: 'root' })
export class RisksApiService {
  private readonly http = inject(HttpClient);

  list(query: RiskQuery): Observable<RiskSummary[]> {
    return this.http.get<RiskRowDto[]>(endpoint, { params: apiQuery(query) }).pipe(map(rows => rows.map(summary)));
  }

  board(query: RiskQuery): Observable<RiskColumn[]> {
    return this.http.get<RiskColumnDto[]>(`${endpoint}/board`, { params: { ...apiQuery(query), offset: 0 } }).pipe(
      map(columns => columns.map(column => mapColumn(column, 0))),
    );
  }

  boardColumn(query: RiskQuery, status: RiskStatus, offset: number): Observable<RiskColumn> {
    const params = { ...apiQuery(query), status, offset, limit: pageSize };
    return this.http.get<RiskColumnDto>(`${endpoint}/board/${status}`, { params }).pipe(
      map(column => mapColumn(column, offset)),
    );
  }

  details(id: string): Observable<RiskDetails> {
    return this.http.get<RiskCardDto>(riskUrl(id)).pipe(
      map(({ data, previousAssessment, passportEditable, jiraRetryPending, reviews }) => ({
        ...summary(data), description: data.risk.description, category: data.categoryName,
        hazard: data.hazardName, asset: data.assetName, author: data.authorName,
        createdAt: data.risk.createdAt, version: data.risk.version, hazardId: data.risk.hazardId,
        previousAssessment, passportEditable, acceptanceLevel: data.risk.acceptanceLevel, closeReason: data.risk.closeReason,
        jiraRetryPending,
        needsHumanAssessment: reviews.some(item => item.review.status === 'WAITING'),
      })),
    );
  }

  create(passport: PassportFields, processId: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(endpoint, { ...passport, processId });
  }

  update(id: string, passport: Passport): Observable<{ id: string }> {
    return this.http.put<{ id: string }>(riskUrl(id), passport);
  }

  references(kind: 'HazardGroups' | 'Hazards' | 'Categories'): Observable<RiskReference[]> {
    return this.options<RiskReference>(kind);
  }

  processes(): Observable<ProcessOption[]> {
    return this.options<ProcessOption>('processes');
  }

  assets(): Observable<RiskOption[]> {
    return this.options<RiskOption>('assets');
  }

  comments(id: string, offset: number, subject: 'risks' | 'treatments' = 'risks'): Observable<RiskComment[]> {
    return this.http.get<RiskComment[]>(commentUrl(subject, id), { params: { offset, limit: historyPageSize } });
  }

  saveComment(riskId: string, text: string, edit?: { id: string; version: string }, subject: 'risks' | 'treatments' = 'risks'): Observable<RiskComment> {
    const url = commentUrl(subject, riskId);
    if (edit) {
      return this.http.put<RiskComment>(`${url}/${encodeURIComponent(edit.id)}`, { text, version: edit.version });
    }
    return this.http.post<RiskComment>(url, { text });
  }

  deleteComment(riskId: string, comment: RiskComment, subject: 'risks' | 'treatments' = 'risks'): Observable<void> {
    return this.http.delete<void>(`${commentUrl(subject, riskId)}/${encodeURIComponent(comment.id)}`,
      { params: { version: comment.version } });
  }

  reviews(id: string, offset: number): Observable<RiskReview[]> {
    return this.http.get<RiskReview[]>(`${riskUrl(id)}/reviews`, { params: { offset, limit: historyPageSize } });
  }

  matrix(): Observable<AssessmentMatrix> {
    return this.http.get<AssessmentMatrix>(`${assessmentSettings}/matrix`);
  }

  scale(id: string): Observable<ImpactScale> {
    return this.http.get<ImpactScale>(`${assessmentSettings}/impact-scale/${encodeURIComponent(id)}`);
  }

  assessments(id: string, offset: number): Observable<Assessment[]> {
    return this.http.get<Assessment[]>(`${riskUrl(id)}/assessments`, { params: { offset, limit: historyPageSize } });
  }

  preview(id: string, inputs: AssessmentInputs): Observable<AssessmentPreview> {
    return this.http.post<AssessmentPreview>(`${riskUrl(id)}/assessments/preview`, inputs);
  }

  saveAssessment(id: string, body: {
    inputs: AssessmentInputs; version: string; scaleVersionId: string; assetVersion: string | null;
  }): Observable<AssessmentPreview> {
    return this.http.post<AssessmentPreview>(`${riskUrl(id)}/assessments`, body);
  }

  decision(id: string, action: RiskAction, body: RiskActionBody): Observable<unknown> {
    return this.http.post(`${riskUrl(id)}/${action}`, body);
  }

  decisionProcesses(id: string, action: 'split' | 'transfer'): Observable<(RiskOption & { ownerName: string | null })[]> {
    return this.readOptions(`${riskUrl(id)}/${action}/processes`);
  }

  /** Справочник может занимать несколько страниц. Не обрезаем доступные варианты. */
  private options<T extends RiskOption>(route: string): Observable<T[]> {
    return this.readOptions<T>(`${referenceEndpoint}/${route}`);
  }

  private readOptions<T extends RiskOption>(url: string): Observable<T[]> {
    const read = (offset: number) => this.http.get<T[]>(url, {
      params: { offset, limit: referencePageSize },
    });
    return read(0).pipe(
      expand((items, index) => items.length === referencePageSize ? read((index + 1) * referencePageSize) : EMPTY),
      reduce((result, items) => result.concat(items), [] as T[]),
    );
  }
}

function riskUrl(id: string): string {
  return `${endpoint}/${encodeURIComponent(id)}`;
}

function mapColumn(dto: RiskColumnDto, offset: number): RiskColumn {
  return { status: dto.status, count: dto.count, total: dto.totalCount, offset, items: dto.items.map(summary) };
}

function summary(dto: RiskRowDto): RiskSummary {
  // Проекция API содержит лишь действующие связи. Владелец может быть неактивен.
  const primary = dto.processes.find(process => process.isPrimary);
  return {
    id: dto.risk.id, name: dto.risk.name, status: dto.risk.status, updatedAt: dto.risk.updatedAt,
    process: primary?.processName ?? null, owner: primary?.ownerName ?? null, currentAssessment: dto.currentAssessment,
    processes: dto.processes, categoryId: dto.risk.categoryId, categoryName: dto.categoryName,
    assetId: dto.risk.assetId, assetName: dto.assetName,
  };
}

function commentUrl(subject: 'risks' | 'treatments', id: string): string {
  return `${api}/${subject}/${encodeURIComponent(id)}/comments`;
}

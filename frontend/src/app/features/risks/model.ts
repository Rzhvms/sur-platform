import type { ParamMap, Params } from '@angular/router';
import { statuses, type RiskStatus } from '../../shared/text';

export const riskStatuses = Object.keys(statuses) as RiskStatus[];
import { pageSize, validOffset } from './board-page';
export { pageSize } from './board-page';
export const maxPage = 1 + Math.floor(2147483647 / pageSize);
export const guid = /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i;

export interface Assessment {
  id?: string;
  likelihoodLevelId?: string;
  impactLevelId?: string;
  scaleVersionId?: string | null;
  isActive?: boolean;
  responseMode?: string | null;
  downtimeTotalHours?: number | null;
  hourlyDowntimeCostSnapshot?: number | null;
  riskScore: number;
  lossAmount: number | null;
  createdAt: string;
}

export interface RiskProcess {
  riskProcessId?: string;
  processId: string;
  processName: string;
  directorateId: string;
  directorateName: string;
  ownerId: string;
  ownerName: string | null;
  ownerUserId?: string;
  ownerActive?: boolean;
  isPrimary: boolean;
}

export interface RiskOption { id: string; name: string }
export interface RiskReference extends RiskOption { groupId: string | null }
export interface RiskReview {
  review: { id: string; status: string; processedAt: string; closeReason: string | null; errorMessage: string | null };
  run: {
    id: string; status: string; reason: string; sourceType: string; sourceNameSnapshot: string | null;
    externalSystemNameSnapshot: string | null; occurredAt: string; scheduledDate: string | null;
  };
}
export interface RiskComment { id: string; version: string; authorId: string; text: string; isSystem: boolean; createdAt: string; updatedAt: string }
export interface PassportFields { name: string; description: string; hazardId: string; categoryId: string; assetId: string | null }
export interface Passport extends PassportFields { version: string; processId?: string }

export interface RiskSummary {
  id: string;
  name: string;
  status: RiskStatus;
  process: string | null;
  owner: string | null;
  currentAssessment: Assessment | null;
  updatedAt: string;
  processes: RiskProcess[];
  categoryId: string;
  categoryName: string;
  assetId: string | null;
  assetName: string | null;
}

export interface RiskDetails extends RiskSummary {
  description: string;
  category: string;
  hazard: string;
  asset: string | null;
  author: string | null;
  createdAt: string;
  previousAssessment: Assessment | null;
  hazardId: string;
  version: string;
  passportEditable: boolean;
  acceptanceLevel?: string | null;
  closeReason?: string | null;
  needsHumanAssessment?: boolean;
  jiraRetryPending?: boolean;
}

export interface RiskColumn { status: RiskStatus; total: number; count: number; offset: number; items: RiskSummary[] }

export const dimensions = ['directorateId', 'processId', 'ownerId', 'assetId', 'categoryId'] as const;
export type Dimension = (typeof dimensions)[number];

export interface RiskQuery extends Partial<Record<Dimension, string>> {
  search: string;
  status: RiskStatus | '';
  sort: 'recent' | 'name';
  page: number;
  offsets?: Partial<Record<RiskStatus, number>>;
}

export function readQuery(params: ParamMap): RiskQuery {
  const rawStatus = params.get('status');
  const rawPage = Number(params.get('page'));
  const filters: RiskQuery = {
    search: (params.get('search') ?? '').trim().slice(0, 250),
    status: riskStatuses.find(status => status === rawStatus) ?? '',
    sort: params.get('sort') === 'name' ? 'name' : 'recent',
    page: Number.isSafeInteger(rawPage) && rawPage > 0 && rawPage <= maxPage ? rawPage : 1,
  };
  for (const name of dimensions) {
    const value = params.get(name);
    if (value && guid.test(value)) {
      filters[name] = value;
    }
  }
  for (const status of riskStatuses) {
    const offset = Number(params.get(`column_${status}`));
    if (offset > 0 && validOffset(offset)) {
      (filters.offsets ??= {})[status] = offset;
    }
  }
  return filters;
}

export function urlQuery({ offsets, ...query }: RiskQuery): Params {
  return {
    ...query,
    ...Object.fromEntries(dimensions.map(key => [key, query[key]?.trim() || null])),
    ...Object.fromEntries(riskStatuses.map(status => [`column_${status}`, offsets?.[status] || null])),
    search: query.search || null,
    status: query.status || null,
    sort: query.sort === 'recent' ? null : query.sort,
    page: query.page === 1 ? null : query.page,
  };
}

export function apiQuery({ page, status, search, offsets, ...rest }: RiskQuery): Record<string, string | number> {
  return {
    ...rest, ...(status ? { status } : {}), ...(search ? { search } : {}),
    offset: (page - 1) * pageSize, limit: pageSize,
  };
}

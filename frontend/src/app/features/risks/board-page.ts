/** Параметры offset/limit в OpenAPI ограничены целыми числами PostgreSQL и страницей до 100. */
export const pageSize = 8;
const maxOffset = 2_147_483_647;

export function validOffset(value: number): boolean {
  return Number.isSafeInteger(value) && value >= 0 && value <= maxOffset && value % pageSize === 0;
}

export function hasNextColumnPage(offset: number, count: number, totalCount: number): boolean {
  return count > 0 && offset + count < totalCount && validOffset(offset + pageSize);
}

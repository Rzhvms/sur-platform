export interface CsvPreview { rows: string[][]; invalid: boolean }

/** Читает ограниченный предпросмотр. Пробелы, многострочные поля и экранированные кавычки остаются в данных. */
export function previewCsv(source: string, delimiter: ',' | ';', limit = 8): CsvPreview {
  const rows: string[][] = [];
  let row: string[] = [];
  let value = '';
  let quoted = false;
  let closed = false;
  let present = false;
  const finishField = () => {
    row.push(value);
    value = '';
    closed = false;
  };
  const finishRow = () => {
    if (present) { finishField(); rows.push(row); }
    row = [];
    present = false;
    value = '';
    closed = false;
  };
  const start = source.charCodeAt(0) === 0xFEFF ? 1 : 0;
  for (let index = start; index < source.length && rows.length < limit; index++) {
    const character = source[index];
    if (quoted) {
      if (character !== '"') { value += character; }
      else if (source[index + 1] === '"') { value += '"'; index++; }
      else { quoted = false; closed = true; }
    } else if (character === delimiter) {
      present = true;
      finishField();
    } else if (character === '\r' || character === '\n') {
      if (character === '\r' && source[index + 1] === '\n') { index++; }
      finishRow();
    } else if (closed) {
      if (character !== ' ' && character !== '\t') { return { rows, invalid: true }; }
    } else if (character === '"' && !value.trim()) {
      quoted = true;
      present = true;
      value = '';
    } else {
      value += character;
      present ||= character.trim() !== '';
    }
  }
  if (quoted) { return { rows, invalid: true }; }
  if (rows.length < limit && present) { finishRow(); }
  return { rows, invalid: false };
}

export const csvFields = {
  Name: 'Название риска', Description: 'Описание', HazardId: 'ID опасности', CategoryId: 'ID категории',
  ProcessId: 'ID процесса', AssetId: 'ID объекта',
} as const;
export type CsvField = keyof typeof csvFields;

/** Обязательные поля паспорта, которым всегда назначается отдельный столбец. */
export const requiredCsvFields: readonly CsvField[] = ['Name', 'Description', 'HazardId', 'CategoryId', 'ProcessId'];

const headerAliases: Record<CsvField, readonly string[]> = {
  Name: ['name', 'название', 'названиериска', 'риск'],
  Description: ['description', 'описание'],
  HazardId: ['hazardid', 'hazard', 'опасность', 'idопасности', 'идопасности'],
  CategoryId: ['categoryid', 'category', 'категория', 'idкатегории', 'идкатегории'],
  ProcessId: ['processid', 'process', 'процесс', 'idпроцесса', 'идпроцесса'],
  AssetId: ['assetid', 'asset', 'объект', 'idобъекта', 'идобъекта'],
};

/** Сопоставляет поля паспорта с колонками по названиям заголовков и заполняет остаток свободными столбцами. */
export function detectColumns(header: readonly string[], columnCount: number): Partial<Record<CsvField, number>> {
  const result: Partial<Record<CsvField, number>> = {};
  const used = new Set<number>();
  const normalized = header.slice(0, columnCount).map(cell => cell.trim().toLowerCase().replace(/[\s\u00a0_.-]+/g, ''));
  for (const field of Object.keys(csvFields) as CsvField[]) {
    const index = normalized.findIndex((cell, position) => !used.has(position) && headerAliases[field].includes(cell));
    if (index >= 0) { result[field] = index; used.add(index); }
  }
  for (const field of requiredCsvFields) {
    if (result[field] !== undefined) { continue; }
    const index = Array.from({ length: columnCount }, (_, position) => position).find(position => !used.has(position));
    if (index === undefined) { break; }
    result[field] = index;
    used.add(index);
  }
  return result;
}

export function mappedColumns(raw: Record<CsvField, string>): Record<string, number> | null {
  const result: Record<string, number> = {};
  const seen = new Set<number>();
  for (const field of Object.keys(csvFields) as CsvField[]) {
    if (field === 'AssetId' && raw[field] === '') { continue; }
    const index = Number(raw[field]);
    if (!/^\d+$/.test(raw[field]) || index >= 128 || seen.has(index)) { return null; }
    result[field] = index;
    seen.add(index);
  }
  return result;
}

export async function describeCsv(file: Blob): Promise<{ source: string; size: number; checksum: string }> {
  const bytes = await file.arrayBuffer();
  const checksum = await crypto.subtle.digest('SHA-256', bytes);
  return { source: new TextDecoder('utf-8', { fatal: true }).decode(bytes), size: file.size,
    checksum: Array.from(new Uint8Array(checksum), value => value.toString(16).padStart(2, '0')).join('') };
}

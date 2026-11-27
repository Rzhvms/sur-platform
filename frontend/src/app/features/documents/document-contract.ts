export type DocumentKind = 'REPORT' | 'HISTORY' | 'IMPORT';
export type ReportFormat = 'CSV' | 'XLSX';
export type DocumentState = 'UPLOADING' | 'AWAITING_MAPPING' | 'PENDING' | 'PROCESSING' | 'COMPLETED' | 'COMPLETED_WITH_ERRORS' | 'FAILED';

export interface DocumentJob {
  id: string; kind: DocumentKind; state: DocumentState; fileId: string | null;
  createdAt: string; authorId: string; asOf: string | null; capturedAt: string | null;
  generatedAt: string | null; completedAt: string | null; error: string | null;
  canRetry: boolean; processed: number; errors: number;
}
export type JsonRecord = Record<string, unknown>;
export interface DocumentRow { number: number; data: JsonRecord; hasError: boolean }
export interface ReportFilters { asOf: string | null; processId: string | null; directorateId: string | null; categoryId: string | null; status: string | null; format: ReportFormat | null }
export interface DatePeriod { from: string; to: string }
export interface ImportSource { size: number; checksum: string }
export interface ImportMapping { delimiter: ',' | ';'; hasHeader: boolean; columns: Record<string, number> }
export type Operation<T> = Readonly<T & { operationId: string }>;

export const maxCsvBytes = 8 * 1024 * 1024;
export const documentPageSize = 25;
export const documentRights = { view: 'view_report', export: 'export_report', import: 'import_data', create: 'create_risk' } as const;

export const documentText = {
  reports: 'Отчёты', reportsHint: 'Сформируйте срез реестра рисков по выбранным фильтрам и выгрузите его в CSV или XLSX.', snapshotParams: 'Параметры среза',
  importFormat: 'CSV · UTF-8',
  importIntro: 'Загрузите риски из CSV: выберите файл, проверьте колонки и запустите импорт.',
  importSteps: 'Этапы импорта', importFileStep: 'Выбор файла', importFileStepHint: 'Подготовьте CSV по шаблону',
  importMappingStep: 'Сопоставление колонок', importMappingStepHint: 'Проверьте поля и первые записи',
  importResultStepHint: 'Проверьте созданные риски и ошибки',
  importFileHint: 'Выберите файл на компьютере. Перед отправкой можно проверить его содержимое.',
  importEncodingHint: 'Кодировка UTF-8', importSizeHint: 'Размер файла до 8 МиБ',
  importReferenceHint: 'В шаблон подставляются доступные справочники вашей организации.',
  importGuide: 'Подготовка файла', importTemplateHint: 'Начните с шаблона: сохраните названия колонок и добавьте свои риски.',
  importRequirements: 'Требования к колонкам',
  importMappingHint: 'Укажите, каким полям риска соответствуют колонки CSV. Для обязательных полей выберите разные колонки.',
  importPreviewHint: 'Здесь показаны первые записи. Полная проверка выполняется при обработке файла.',
  imports: 'Импорт CSV', history: 'Журнал изменений', view: 'Просмотреть снимок',
  csv: 'Скачать CSV', xlsx: 'Скачать XLSX', json: 'Подготовить JSON',
  from: 'С даты UTC', to: 'По дату UTC включительно', historical: 'Фильтр по дате', localTime: 'Дата и время среза',
  offset: 'Смещение UTC', asOf: 'Момент среза', captured: 'Фиксация снимка', generated: 'Формирование файла',
  completed: 'Завершено', requested: 'Регистрация задания', author: 'Автор', totalProcessed: 'Обработано строк',
  totalErrors: 'Отклонено строк', imported: 'Созданные риски', row: 'Запись', results: 'Результаты импорта',
  details: 'Подробности', chooseFile: 'Выберите CSV до 8 МиБ', chooseSame: 'Выберите исходный CSV для этого задания.',
  tooLarge: 'Выберите непустой CSV размером до 8 МиБ.', utf8: 'Файл должен быть в кодировке UTF-8.',
  checksum: 'SHA-256', size: 'Размер в байтах', preparing: 'Чтение файла и вычисление SHA-256…',
  register: 'Зарегистрировать импорт', upload: 'Загрузить CSV', start: 'Запустить импорт',
  preview: 'Предпросмотр первых записей', delimiter: 'Разделитель', comma: 'Запятая', semicolon: 'Точка с запятой',
  header: 'Первая запись содержит заголовки', column: 'Столбец', noAsset: 'Без объекта',
  invalidMapping: 'Выберите разные столбцы для всех обязательных полей.',
  unknownCommand: 'Ответ на команду не получен. Повтор отправит те же параметры.',
  sameFile: 'Для продолжения используйте тот же CSV, который был зарегистрирован.',
  importHint: 'CSV должен содержать колонки Name, Description, HazardId, CategoryId, ProcessId. Идентификаторы справочников — UUID. Выгрузка отчёта для импорта не подходит.',
  template: 'Скачать шаблон CSV', templateEmpty: 'Нет доступных справочников для шаблона.',
  checkField: 'Проверьте поле', invalidRow: 'Строка не соответствует формату импорта.',
  referenceMissing: 'Справочник не найден: проверьте ID опасности, категории и процесса.',
  referenceInactive: 'Справочник неактивен.', importAccessDenied: 'Нет права регистрировать риск в дирекции этого процесса.',
  loadPreviewFirst: 'Загрузите исходный CSV, чтобы выбрать столбцы.',
  createNew: 'Начать новое задание', file: 'Скачать файл', downloadProgress: 'Скачивание файла…',
  status: 'Статус задания', noRows: 'Выборка пуста.', invalidPeriod: 'Укажите корректный диапазон дат UTC.',
  invalidSnapshot: 'Проверьте дату, время, смещение UTC и фильтры среза.',
  event: 'Событие', entity: 'Сущность', date: 'Время UTC', mapPermission: 'Для запуска импорта также требуется право регистрации риска.',
  readPreview: 'Загрузить исходный CSV для предпросмотра', invalidCsv: 'Предпросмотр остановлен на некорректной CSV-записи. Сервер проверит файл полностью.',
  wrongJob: 'Задание другого типа. Откройте его в соответствующем разделе.',
  noPreview: 'Предпросмотр недоступен.', retryJob: 'Повторить обработку',
  conflict: 'Задание или его параметры изменились. Обновите состояние перед повтором.',
  pending: 'Ожидает обработки', processing: 'Обрабатывается', awaitingUpload: 'Ожидает файла',
  awaitingMapping: 'Ожидает сопоставления', completedPartial: 'Завершено с ошибками строк', failed: 'Обработка остановлена',
} as const;

export const stateLabels: Record<DocumentState, string> = {
  UPLOADING: documentText.awaitingUpload, AWAITING_MAPPING: documentText.awaitingMapping, PENDING: documentText.pending,
  PROCESSING: documentText.processing, COMPLETED: documentText.completed, COMPLETED_WITH_ERRORS: documentText.completedPartial, FAILED: documentText.failed,
};

export function operation<T extends object>(snapshot: T): Operation<T> {
  return Object.freeze({ ...structuredClone(snapshot), operationId: crypto.randomUUID() });
}

export function calendarDay(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value) || value.startsWith('0000')) { return false; }
  const date = new Date(value + 'T00:00:00Z');
  return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

export function snapshotTime(local: string, offset: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}T(?:[01]\d|2[0-3]):[0-5]\d(?::[0-5]\d)?$/.test(local)
    || !calendarDay(local.slice(0, 10)) || !/^[+-](?:(?:0\d|1[0-3]):[0-5]\d|14:00)$/.test(offset)) { return null; }
  return local + (local.length === 16 ? ':00' : '') + offset;
}

export function canPoll(state: DocumentState): boolean {
  return state === 'PENDING' || state === 'PROCESSING';
}

/** Вложение получает только ожидаемое расширение и локальное безопасное имя. */
export function safeFilename(disposition: string | null, extension: string): string {
  const fallback = 'document.' + extension;
  const encoded = disposition?.match(/(?:^|;)\s*filename\*\s*=\s*UTF-8''([^;]+)/i)?.[1];
  const regular = disposition?.match(/(?:^|;)\s*filename\s*=\s*(?:"([^"]*)"|([^;]+))/i);
  let name = regular?.[1] ?? regular?.[2] ?? '';
  if (encoded) {
    try { name = decodeURIComponent(encoded.trim()); } catch { return fallback; }
  }
  name = name.trim();
  if (!name.toLowerCase().endsWith('.' + extension)) { return fallback; }
  const stem = name.slice(0, -(extension.length + 1)).replace(/[^\p{L}\p{N}_ -]/gu, '_').trim().slice(0, 96);
  return stem && !/^(?:con|prn|aux|nul|com[1-9]|lpt[1-9])$/i.test(stem) ? stem + '.' + extension : fallback;
}

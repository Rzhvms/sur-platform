export interface TreatmentDraft {
  title: string;
  description: string | null;
  dueDate: string;
  costPlanned: number;
  costActual: number | null;
}

export interface Treatment extends TreatmentDraft {
  id: string;
  riskId: string;
  riskProcessId: string;
  processId: string;
  version: string;
  status: string;
}

export interface Approval {
  id: string;
  version: string;
  treatmentId: string;
  status: string;
  requiredLevel: string;
  costPlanned: number;
  triggerThreshold: number;
  ownerThreshold: number;
  directorThreshold: number;
  ceoThreshold: number;
  requestedAt: string;
  decisionReason: string | null;
  revokeReason: string | null;
}

/** Страница общего списка мер текущего клиента без вымышленного общего количества */
export interface TreatmentRegistry {
  items: Treatment[];
  hasMore: boolean;
  riskNames: Record<string, string>;
  processNames: Record<string, string>;
}

export interface TreatmentQueue { items: Approval[]; count: number; treatmentTitles?: Record<string, string> }

export interface JiraDelivery {
  state: string | null;
  issueKey: string | null;
  issueUrl: string | null;
  manualControl: boolean;
  attemptStartedAt: string | null;
  jiraRetryPending?: boolean;
}

export interface TreatmentCard {
  treatment: Treatment;
  requiredApprovalLevel: string | null;
  latestApproval: Approval | null;
  assignment: { assigneeId: string; assignedAt: string } | null;
  assigneeName?: string | null;
  jira: JiraDelivery;
}

export type Decision = 'approve' | 'reject' | 'revoke';
export type Transition = 'hold' | 'resume' | 'complete' | 'cancel';
export interface Executor { personId: string; fullName: string }

export const approvalRights = ['approve_owner_treatment', 'approve_director_treatment', 'approve_ceo_treatment'] as const;
export const treatmentPageSize = 25;
export const treatmentStatuses: Record<string, string> = {
  DRAFT: 'Черновик', PENDING: 'На согласовании', APPROVED: 'Согласовано', WAITING: 'Ожидает выполнения',
  IN_PROGRESS: 'Выполняется', HOLD: 'Приостановлена', DONE: 'Завершена', CANCELLED: 'Отменена',
  REJECTED: 'Отклонено', REVOKED: 'Отозвано',
};
export const treatmentText = {
  confirmDecision: 'Подтвердить решение',
  approveConfirm: 'Подтвердите согласование меры по сохранённому уровню и плановым расходам.',
  rejectConfirm: 'Укажите причину отказа. Мера вернётся в черновик для доработки.',
  revokeConfirm: 'Укажите причину отзыва. Запрос на согласовании вернёт меру в черновик, а отзыв принятого согласования отменит меру.',
  savedThresholds: 'Пороги на момент запроса',
  awaitingApproval: 'Ожидается согласование', openApprovals: 'Открыть согласования',
  approvalQueueHint: 'Решение принимается в разделе «Согласования». Откройте очередь, найдите эту меру и выберите «Согласовать» или «Отклонить».',
  approvalAccessHint: 'Для согласования нужно право и действующее назначение требуемого уровня. Администратор также может принять решение.',
  approvals: 'Согласования', queue: 'Ожидают решения', noApprovals: 'Нет запросов на согласование',
  approvalHistory: 'История согласований', level: 'Уровень согласования', trigger: 'Порог для запроса',
  ownerThreshold: 'Порог владельца процесса', directorThreshold: 'Порог директора', ceoThreshold: 'Порог генерального директора',
  registryHint: 'Меры по всем рискам. Для создания новой меры откройте карточку нужного риска.',
  registryEmpty: 'Мер пока нет. Выберите риск и создайте первую меру в его карточке.',
  registryUnavailable: 'Список мер временно недоступен. Повторите загрузку позже.',
  registryRisk: 'Связанный риск', toRegistry: 'Вернуться к списку мер',
  manageHeading: 'Выберите риск для работы с мерами',
  manageFromRisk: 'Откройте карточку риска, чтобы найти его меры или создать черновик.',
  toRisks: 'Открыть реестр рисков', toRisk: 'Вернуться к мерам риска', create: 'Создать меру',
  start: 'Запустить меры', startHint: 'Для запуска нужна хотя бы одна ожидающая мера. Все активные меры должны ожидать выполнения.',
  startConfirm: 'Запустить все ожидающие меры риска?', draft: 'Черновик меры', saved: 'Изменение сохранено.',
  compareDraft: 'Продолжить с актуальной версией', plan: 'Плановые расходы', actual: 'Фактические расходы',
  submit: 'Отправить на согласование', approve: 'Согласовать', reject: 'Отклонить', revoke: 'Отозвать',
  reason: 'Причина', reasonRequired: 'Укажите причину.', confirmTransition: 'Подтверждаю переход', discardDrafts: 'Если это последняя активная мера, сервер отменит оставшиеся черновики и откроет пересмотр риска. Подтвердите переход.',
  noEligibleExecutors: 'Нет доступных исполнителей. Проверьте активных сотрудников и их принадлежность к допустимой дирекции.',
  executor: 'Исполнитель', noExecutor: 'Исполнитель не назначен', chooseExecutor: 'Выберите исполнителя',
  loadExecutors: 'Загрузить исполнителей', dispatch: 'Назначить и передать в ожидание', assignment: 'Изменить исполнителя',
  unassign: 'Снять исполнителя', hold: 'Приостановить', resume: 'Возобновить', complete: 'Завершить', cancel: 'Отменить меру',
  manual: 'Ручное управление', external: 'Управление через Jira', jiraPending: 'Ожидается отправка задачи в Jira.',
  jiraSending: 'Задача отправляется в Jira.', jiraDeferred: 'Создание задачи отложено.',
  jiraUnknown: 'Результат создания задачи неизвестен. Перед повтором нужна сверка в Jira.',
  riskRetry: 'Запуск мер сохранён. Jira временно недоступна. Отправка ожидает автоматического повтора.',
  measureRetry: 'Возобновление меры сохранено. Jira временно недоступна. Отправка ожидает автоматического повтора.',
  reviewAttempt: 'Сверить попытку',
  issue: 'Ключ или ID найденной задачи', linkIssue: 'Связать задачу', confirmAbsent: 'Подтверждаю, что задачи в Jira нет',
  resolveAbsent: 'Сохранить результат сверки', attempt: 'Начало попытки', refreshError: 'Не удалось обновить карточку.',
  currentRisk: 'Состояние риска', openTreatment: 'Открыть меру', treatment: 'Мера',
  slotTaken: 'Для этого процесса уже существует мера. Обновите список и выберите её.',
  invalidExecutor: 'Исполнитель больше не доступен. Выберите другого.',
  stateConflict: 'Действие недоступно в текущем состоянии. Сверьте обновлённые данные.',
  noApprover: 'Для этого уровня не найден действующий согласующий.',
  resolutionConflict: 'Состояние Jira изменилось. Сверьте текущую попытку перед повтором.',
} as const;

/** Принимаем только абсолютную HTTPS-ссылку без встроенных реквизитов. */
export function jiraLink(value: string | null): string | null {
  if (!value || !/^https:\/\//i.test(value)) {
    return null;
  }
  try {
    const url = new URL(value);
    return url.protocol === 'https:' && !url.username && !url.password ? url.href : null;
  } catch {
    return null;
  }
}

/** Срок является календарной датой. Пустая фактическая сумма сохраняется как null. */
export function validTreatmentDraft(draft: TreatmentDraft): boolean {
  const date = new Date(draft.dueDate + 'T00:00:00Z');
  return !!draft.title.trim() && /^\d{4}-\d{2}-\d{2}$/.test(draft.dueDate)
    && !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === draft.dueDate
    && Number.isFinite(draft.costPlanned) && draft.costPlanned >= 0
    && (draft.costActual === null || (Number.isFinite(draft.costActual) && draft.costActual >= 0));
}

export function approvalLevel(value: string): string {
  const labels: Record<string, string> = { OWNER: 'Владелец процесса', DIRECTOR: 'Директор', CEO: 'Генеральный директор' };
  return labels[value.toUpperCase()] ?? value;
}

/** Отказ и отзыв принимают только содержательную причину. */
export function decisionReason(action: Decision, input: string): string | null {
  const reason = input.trim();
  return reason || (action === 'approve' ? '' : null);
}

/** Отправляем один результат сверки и точный timestamp из актуальной карточки. */
export function jiraResolution(jira: JiraDelivery, reviewed: string | null, issue: string, absent: boolean) {
  if (jira.state !== 'UNKNOWN' || !reviewed || reviewed !== jira.attemptStartedAt) {
    return null;
  }
  if (absent) {
    return { attemptStartedAt: reviewed, issue: null, confirmAbsent: true };
  }
  const key = issue.trim();
  return key ? { attemptStartedAt: reviewed, issue: key, confirmAbsent: false } : null;
}

/** Некорректный offset не отправляется в API и не создаёт дробные страницы */
export function treatmentRegistryOffset(value: string | null): number {
  const maximumOffset = 2_147_483_647;
  const offset = Number(value ?? 0);
  return Number.isSafeInteger(offset) && offset >= 0 && offset <= maximumOffset
    && offset % treatmentPageSize === 0 ? offset : 0;
}

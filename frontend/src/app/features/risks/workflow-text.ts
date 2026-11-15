export const workflow = {
  assessment: 'Оценка риска', history: 'История оценок', emptyHistory: 'Сохранённых оценок пока нет',
  matrix: 'Матрица риска', likelihood: 'Вероятность', impact: 'Тяжесть',
  likelihoodScale: 'Строки — вероятность, столбцы — тяжесть.',
  scale: 'Денежная шкала', assessmentScale: 'Шкала на момент оценки', noScale: 'Для этой записи версия шкалы отсутствует.',
  scaleVersion: 'Версия шкалы', emptyRange: 'Пустой диапазон', noLimit: 'и выше',
  amount: 'Денежный ущерб', weeks: 'Недели простоя', days: 'Дни простоя', hours: 'Часы простоя',
  totalHours: 'Всего часов простоя', rate: 'Стоимость часа на момент оценки',
  preview: 'Рассчитать предварительно', saveAssessment: 'Подтвердить оценку', previewHeading: 'Предварительный расчёт',
  reviewRequired: 'Требуется оценка владельца процесса', reviewComplete: 'Запуск завершён',
  assessmentConflict: 'Риск, объект или шкала изменились. Получите новый расчёт и подтвердите его.',
  refreshRisk: 'Обновить версию риска', noPublishedScale: 'Сначала администратор должен опубликовать денежную шкалу.',
  actions: 'Решения и переходы', selectAction: 'Выберите действие',
  noActions: 'Нет доступных действий для ваших прав и состояния риска.',
  processSelection: 'Выберите минимум два процесса и отметьте один основной.',
  targetProcess: 'Целевой процесс', noneAvailable: 'Нет доступных процессов для выбранной операции.',
  reason: 'Причина закрытия', reopenConfirm: 'Подтверждаю возврат закрытого риска на пересмотр.',
  blockers: 'Меры, блокирующие действие', invalidState: 'Действие недоступно в текущем состоянии риска.',
  alreadyDecided: 'Для текущей оценки уже выбрано решение.', inactiveProcess: 'Один из выбранных процессов недоступен.',
  source: 'Источник пересмотра', runStatus: 'Состояние запуска', run: 'Запуск', reasonLabel: 'Причина', scheduled: 'Дата по расписанию',
  acceptance: 'Уровень принятия', resultMode: 'Выбранное решение',
} as const;

export const actionLabels = {
  accept: 'Принять риск', treat: 'Выбрать обработку', split: 'Разделить риск',
  transfer: 'Передать риск', close: 'Закрыть риск', reopen: 'Вернуть на пересмотр',
} as const;

export const acceptanceLabels: Record<string, string> = {
  OWNER: 'Владелец процесса', DIRECTOR: 'Директор', CEO: 'Генеральный директор',
  TREATMENT_REQUIRED: 'Требуется обработка',
};

export const responseModeLabels: Record<string, string> = {
  accept: 'Принятие', treat: 'Обработка', split: 'Разделение',
};

export const reviewReasons: Record<string, string> = {
  MANUAL: 'Ручной пересмотр', AFTER_TREATMENT: 'После выполнения меры', SCHEDULE: 'По расписанию', WEBHOOK: 'Внешнее событие',
};

export const reviewSources: Record<string, string> = {
  RISK_CARD: 'Карточка риска', TRANSFER: 'Передача риска', TREATMENT: 'Мера',
  REVIEW_SCHEDULE: 'Расписание', REVIEW_WEBHOOK: 'Внешняя система',
};

import { RiskStatus } from '../../shared/text';
import { RiskDetails, RiskQuery, pageSize, riskStatuses } from './model';

type Example = [name: string, process: string, owner: string, status: RiskStatus, score: number | null, loss: number, hazard: string];

const examples: Example[] = [
  ['Отказ основной системы охлаждения в серверном помещении', 'Эксплуатация инфраструктуры', 'Анна Лебедева', 'IN_PROGRESS', 20, 8400000, 'Перегрев оборудования'],
  ['Задержка поставок комплектующих для критического оборудования', 'Закупки и снабжение', 'Илья Соколов', 'ACTIVE', 16, 3900000, 'Срыв срока поставки'],
  ['Несанкционированный доступ к архиву персональных данных', 'Информационная безопасность', 'Ольга Волкова', 'AWAITING_REVIEW', null, 2500000, 'Утечка информации'],
  ['Ошибки при сверке управленческой отчётности', 'Финансовый контроль', 'Мария Орлова', 'ACCEPTED', 4, 260000, 'Расхождение данных'],
  ['Отсутствие замены ответственного за контроль подрядчиков', 'Управление подрядчиками', 'Максим Громов', 'DRAFT', null, 0, 'Непрерывность контроля'],
  ['Перебой связи на распределительном участке', 'Логистика и хранение', 'Денис Ершов', 'CLOSED', 6, 480000, 'Отказ канала связи'],
  ['Износ узлов автоматизированной линии упаковки', 'Производственные операции', 'Елена Морозова', 'ACTIVE', 12, 1800000, 'Отказ оборудования'],
  ['Длительный простой системы расчёта с поставщиками', 'Казначейские операции', 'Павел Егоров', 'IN_PROGRESS', 9, 1100000, 'Сбой финансовой системы'],
  ['Зависимость непрерывного производства от единственного поставщика специализированных комплектующих', 'Планирование производственных мощностей и управление снабжением', 'Александра Константинопольская', 'DRAFT', null, 0, 'Концентрация поставок'],
  ['Неполное восстановление данных из резервной копии', 'Управление данными', 'Анна Лебедева', 'AWAITING_REVIEW', null, 600000, 'Потеря данных'],
];

export const exampleRisks: RiskDetails[] = examples.map(([name, process, owner, status, score, loss, hazard], index) => {
  const updatedAt = `2026-11-${String(14 - index).padStart(2, '0')}T10:15:00Z`;
  return {
    id: `R-${1042 - index}`, name, status, process, owner, hazard, updatedAt,
    processes: [{ processId: process, processName: process, isPrimary: true, ownerId: owner, ownerName: owner, directorateId: '', directorateName: '' }],
    categoryId: '', categoryName: '', assetId: null, assetName: null,
    hazardId: '', version: '', passportEditable: false,
    description: index === 0
      ? 'При остановке основной системы охлаждения температура в серверном помещении может превысить допустимый предел. Возможны остановка сервисов и восстановление инфраструктуры с привлечением дежурной команды.'
      : `${name}. Ответственный процесс - «${process}».`,
    category: index < 3 ? 'Операционные' : 'Внутренние процессы',
    asset: index === 0 ? 'Серверное помещение № 2' : null,
    author: owner, createdAt: '2026-10-20T09:00:00Z',
    currentAssessment: score === null ? null : { riskScore: score, lossAmount: loss, createdAt: updatedAt },
    previousAssessment: status === 'AWAITING_REVIEW' ? { riskScore: 12, lossAmount: loss, createdAt: updatedAt } : null,
  };
});

function filtered(query: RiskQuery): RiskDetails[] {
  const search = query.search.toLocaleLowerCase('ru');
  const rows = exampleRisks.filter(row =>
    (!query.status || row.status === query.status) && row.name.toLocaleLowerCase('ru').includes(search),
  );
  return query.sort === 'name' ? rows.sort((a, b) => a.name.localeCompare(b.name, 'ru')) : rows;
}

export function listExample(query: RiskQuery): RiskDetails[] {
  return filtered(query).slice((query.page - 1) * pageSize, query.page * pageSize);
}

export function boardExample(query: RiskQuery) {
  const rows = filtered(query);
  return riskStatuses.map(status => {
    const items = rows.filter(row => row.status === status);
    const offset = query.offsets?.[status] ?? 0;
    const page = items.slice(offset, offset + pageSize);
    return { status, total: items.length, items: page, offset, count: page.length };
  });
}

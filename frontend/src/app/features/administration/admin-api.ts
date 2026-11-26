import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { EMPTY, Observable, expand, finalize, reduce } from 'rxjs';
import { errorMessage, problemCode, providerApi } from '../../core/http/api';
import { text } from '../../shared/text';

export const firstCursor = '00000000-0000-0000-0000-000000000000';
export const adminPageSize = 25;
export type Row = { id: string; name: string; version: string; isActive: boolean };
export type User = {
  userId: string; version: string; fullName: string | null; email: string | null;
  roleIds: string[]; roles: string[]; isActive: boolean; invitationStatus: string | null;
};
export type Role = Row & { code: string; rightIds: string[]; description: string | null };
export type Right = { id: string; code: string; name: string | null };
export type Person = Omit<Row, 'name'> & {
  fullName: string | null; email: string | null; userId: string; directorateId: string | null; positionName: string | null;
};
export type ResponsibilityType = 'CEO' | 'DIRECTOR' | 'RISK_SPECIALIST';
export type Assignment = Omit<Row, 'name'> & {
  responsibilityType: ResponsibilityType; personId: string; directorateId: string | null;
};
export type Process = Row & { directorateId: string; ownerId: string; description: string | null };
export type TreatmentSummary = { id: string; title: string };
export type OrgContext = { person: Person | null; processes: Process[]; responsibilities: Assignment[]; treatments: TreatmentSummary[] };
export type Slot = { assignmentId: string | null; observedAt: string };
type Param = Record<string, string | number | boolean>;

export const adminText = {
  users: 'Пользователи', roles: 'Роли и права', organization: 'Оргструктура', riskSettings: 'Настройки рисков',
  reviews: 'Источники пересмотра', jira: 'Jira Data Center', active: 'Активно', inactive: 'Неактивно',
  create: 'Создать', fullName: 'ФИО', position: 'Должность', employee: 'Сотрудник', accountOnly: 'Только учётная запись',
  directorates: 'Дирекции', persons: 'Сотрудники', processes: 'Процессы', assets: 'Объекты', responsibilities: 'Назначения',
  deactivate: 'Деактивировать', activate: 'Активировать', reissue: 'Повторно выдать доступ', closeAssignment: 'Завершить назначение',
  replaceAssignment: 'Заменить назначение', changeOwner: 'Заменить владельца', transfer: 'Перевести сотрудника',
  transferDetails: 'Текущая запись сотрудника закроется. В новой дирекции будет создана отдельная запись.',
  confirmOwner: 'Замена обновит владельца в действующих связях с рисками и добавит системные комментарии.',
  confirmDeactivate: 'Сессии будут отозваны, активная запись сотрудника будет деактивирована.',
  confirmReissue: 'Будет выдан новый временный доступ. Предыдущий пароль и сессии перестанут действовать.',
  blockers: 'Блокирующие связи', dependencies: 'Сначала завершите активные зависимости.',
  dependencyLink: 'Связанные записи', version: 'Версия', slot: 'Проверить позицию', free: 'Позиция свободна',
  occupied: 'Позиция занята', slotChanged: 'Позиция была занята после проверки. Проверьте доступность ещё раз.',
  activeRole: 'Выберите хотя бы одну действующую роль.', lastAdmin: 'В системе должен остаться действующий администратор.',
  protectedRole: 'Роль ADMIN защищена системными правилами.', roleInUse: 'Сначала назначьте затронутым пользователям другую действующую роль.',
  duplicateEmail: 'Этот адрес уже используется.', orgIneligible: 'Выбранная запись не подходит для операции. Проверьте её статус и роли.',
  saved: 'Изменения сохранены.', conflict: 'Запись изменилась. Сверьте актуальные данные перед сохранением.',
  temporaryPassword: 'Временный пароль', repeatTemporaryPassword: 'Повторите временный пароль',
  temporaryPasswordHint: 'От 8 до 256 символов. Передайте пароль пользователю самостоятельно. Он действует 10 дней и требует смены при первом входе.',
  temporaryPasswordInvalid: 'Введите временный пароль от 8 до 256 символов и подтвердите его.',
  noDirectorate: 'Без дирекции', usersEmpty: 'Пользователей пока нет', rights: 'Права',
} as const;

export const responsibilityLabels: Record<ResponsibilityType, string> = {
  CEO: 'Генеральный директор', DIRECTOR: 'Директор', RISK_SPECIALIST: 'Специалист по оценке рисков',
};

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  get<T>(path: string, params?: Param): Observable<T> {
    return this.http.get<T>(providerApi + '/' + path, { params });
  }

  post<T>(path: string, body: object | null, params?: Param): Observable<T> {
    return this.http.post<T>(providerApi + '/' + path, body, { params });
  }

  put<T>(path: string, body: object, params?: Param): Observable<T> {
    return this.http.put<T>(providerApi + '/' + path, body, { params });
  }

  patch<T>(path: string, body: object): Observable<T> {
    return this.http.patch<T>(providerApi + '/' + path, body);
  }

  delete(path: string, version: string): Observable<unknown> {
    return this.http.delete(providerApi + '/' + path, { params: { version } });
  }

  /** Полный каталог для селектов. Таблицы рабочего раздела загружают по одной странице. */
  options<T>(path: string, key: (row: T) => string, params: Param = {}): Observable<T[]> {
    const size = 100;
    const page = (after: string) => this.get<T[]>(path, { ...params, after, limit: size });
    return page(firstCursor).pipe(
      expand(rows => rows.length === size ? page(key(rows[rows.length - 1])) : EMPTY),
      reduce((all, rows) => all.concat(rows), [] as T[]),
    );
  }

  slot(type: ResponsibilityType, directorateId: string): Observable<Slot> {
    return this.get<Slot>('organization/responsibilities/slot',
      type === 'CEO' ? { responsibilityType: type } : { responsibilityType: type, directorateId });
  }
}

/** Удерживает одну запись в полёте; после неопределённого ответа перечитывает состояние. */
export class AdminWrite {
  readonly busy = signal(false);
  readonly notice = signal('');
  readonly dependencyIds = signal<string[]>([]);

  constructor(private readonly destroy: DestroyRef) {}

  run<T>(request: Observable<T>, done: (value: T) => void, refresh?: (error: unknown) => void): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.notice.set('');
    this.dependencyIds.set([]);
    request.pipe(takeUntilDestroyed(this.destroy), finalize(() => this.busy.set(false))).subscribe({
      next: value => {
        this.notice.set(adminText.saved);
        done(value);
      },
      error: (error: unknown) => {
        this.notice.set(adminError(error));
        if (error instanceof HttpErrorResponse) {
          const ids: unknown = error.error?.dependencyIds;
          this.dependencyIds.set(Array.isArray(ids) ? ids.filter((id): id is string => typeof id === 'string') : []);
          if (error.status === 409 || error.status === 0 || error.status >= 500) {
            refresh?.(error);
          }
        }
      },
    });
  }
}

function adminError(error: unknown): string {
  if (error instanceof HttpErrorResponse) {
    switch (problemCode(error)) {
      case 'LAST_ADMINISTRATOR': return adminText.lastAdmin;
      case 'ADMIN_ROLE_PROTECTED': return adminText.protectedRole;
      case 'ROLE_DEACTIVATION_BLOCKED_LAST_ROLE': return adminText.roleInUse;
      case 'ACTIVE_ROLE_REQUIRED': return adminText.activeRole;
      case 'EMAIL_ALREADY_EXISTS': return adminText.duplicateEmail;
      case 'ORGANIZATION_DEPENDENCIES': return adminText.dependencies;
      case 'ORGANIZATION_INELIGIBLE': return adminText.orgIneligible;
      case 'RESPONSIBILITY_SLOT_OCCUPIED': return adminText.occupied;
      case 'RESPONSIBILITY_SLOT_CHANGED': return adminText.slotChanged;
      case 'STALE_VERSION': return adminText.conflict;
    }
    if (!error.status || error.status >= 500) {
      return text.unknownWrite;
    }
  }
  return errorMessage(error);
}

export function rowPath(path: string, id: string): string {
  return path + '/' + encodeURIComponent(id);
}

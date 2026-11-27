import { DOCUMENT } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EMPTY, catchError, finalize, forkJoin, map, of, startWith, switchMap, tap } from 'rxjs';
import { errorMessage, problemCode } from '../../../core/http/api';
import { utcTime } from '../../../shared/format';
import { text } from '../../../shared/text';
import { DataStateComponent, DataState } from '../../../shared/ui/data-state/data-state.component';
import { jiraLink, treatmentText } from '../../treatments/treatment-contract';
import { pollWhileVisible } from '../../treatments/visible-poll';
import { JiraApiService, JiraLogItem, JiraMetadata, JiraSettings, JiraUser, initialVersion, jiraLogPageSize } from '../jira-api.service';

export const jiraTitle = 'Настройка Jira Data Center';
const jiraText = {
  saved: 'Настройки Jira сохранены.', enabled: 'Обмен включён', baseUrl: 'Базовый HTTPS-адрес',
  project: 'Ключ проекта', issueType: 'ID типа задачи', doneStatus: 'ID статуса завершения',
  holdStatus: 'ID статуса приостановки', cancelStatus: 'ID статуса отмены', responsible: 'Ответственный за интеграцию',
  restToken: 'Токен Jira REST', webhookSecret: 'Секрет webhook', timeZone: 'Календарная зона',
  secrets: 'Для нового подключения укажите оба секрета. Для существующего выберите, какой секрет заменить.',
  replaceRest: 'Заменить токен Jira REST', replaceWebhook: 'Заменить секрет webhook',
  absent: 'Подключение Jira ещё не настроено.', disabled: 'Обмен выключен',
  creator: 'Создатель настройки', editor: 'Последнее изменение', pending: 'Ожидают отправки или отправляются',
  unknown: 'Требуют сверки', retry: 'Состояние повтора', task: 'Задача Jira',
  restoreRetry: 'Интеграция включена. Jira временно недоступна, отправка восстановленных задач ждёт автоматического повтора.',
  discoverProjects: 'Загрузить проекты', discoverFields: 'Загрузить типы задач и статусы',
  invalidData: 'Проверьте HTTPS-адрес, реквизиты и соответствия. Для каждого пользователя нужен уникальный логин Jira.',
  unavailable: 'Jira не ответила на проверку. Проверьте подключение и права токена.',
  editBlocked: 'Адрес, проект или тип задачи уже используются. Проверьте текущую настройку Jira.',
  mappings: 'Соответствия исполнителей', addMapping: 'Добавить соответствие', jiraLogin: 'Логин Jira',
  errors: 'Журнал обмена', noErrors: 'Ошибок обмена нет', operation: 'Операция', code: 'Причина',
  compare: 'Загрузить текущую настройку с сервера', emptyMetadata: 'Доступных значений нет.',
  attempted: 'Создание задачи', sync: 'Синхронизация',
} as const;

const retryLabels: Record<string, string> = {
  PENDING: 'Ожидает повтора', SENDING: 'Отправляется', UNKNOWN: 'Требует сверки', DEFERRED: 'Отложено до возобновления меры',
  PAUSED: 'Обмен отключён', STOPPED: 'Повтор не требуется', LINKED: 'Задача связана',
  SYNC_SCHEDULED: 'Запланирована проверка задачи',
};

function field(value = '') {
  return new FormControl(value, { nonNullable: true });
}

function mapping(userId: string, login: string) {
  return new FormGroup({ userId: field(userId), login: field(login) });
}

@Component({
  imports: [RouterLink, ReactiveFormsModule, DataStateComponent],
  templateUrl: './jira-settings-page.component.html',
})
export class JiraSettingsPageComponent {
  private readonly api = inject(JiraApiService);
  private readonly document = inject(DOCUMENT);
  private readonly destroyRef = inject(DestroyRef);
  private readonly revision = signal(0);
  private readonly logRevision = signal(0);
  private version = initialVersion;
  protected readonly t = text;
  protected readonly w = jiraText;
  protected readonly title = jiraTitle;
  protected readonly treatment = treatmentText;
  protected readonly time = utcTime;
  protected readonly busy = signal(false);
  protected readonly conflicted = signal(false);
  protected readonly notice = signal('');
  protected readonly discoveryError = signal('');
  protected readonly metadata = signal<JiraMetadata | null>(null);
  protected readonly offset = signal(0);
  protected readonly pageSize = jiraLogPageSize;
  protected readonly zone = signal('');
  protected readonly connected = signal(false);
  protected readonly panel = signal<JiraSettings | null>(null);
  protected readonly panelNotice = signal('');
  protected readonly retryLabels = retryLabels;
  protected readonly form = new FormGroup({
    baseUrl: field(), project: field(), issueType: field(), doneStatus: field(), holdStatus: field(),
    cancelStatus: field(), responsibleUserId: field(), restToken: field(), webhookSecret: field(),
    enabled: new FormControl(false, { nonNullable: true }),
    replaceRest: new FormControl(false, { nonNullable: true }), replaceWebhook: new FormControl(false, { nonNullable: true }),
    mappings: new FormArray<ReturnType<typeof mapping>>([]),
  });
  protected readonly selectedType = toSignal(this.form.controls.issueType.valueChanges, { initialValue: '' });
  protected readonly statusOptions = computed(() => this.metadata()?.statuses?.find(type => type.id === this.selectedType())?.statuses ?? []);
  protected readonly config = toSignal(toObservable(this.revision).pipe(switchMap(() =>
    forkJoin({ value: this.api.settings(), users: this.api.users() }).pipe(
      tap(({ value }) => this.populate(value)),
      map(value => ({ kind: 'ready' as const, value })),
      catchError((error: unknown) => of({ kind: 'error' as const, message: errorMessage(error) })),
      startWith({ kind: 'loading' as const }),
    ),
  )), { initialValue: { kind: 'loading' as const } });
  private readonly panelPoll = toSignal(toObservable(this.connected).pipe(switchMap(connected => connected
    ? pollWhileVisible(this.document, () => this.api.settings().pipe(
        tap(value => { this.panel.set(value); this.panelNotice.set(''); }),
        catchError(error => {
          this.panelNotice.set(errorMessage(error));
          return EMPTY;
        }),
      )) : EMPTY)));
  private readonly logQuery = computed(() => ({ offset: this.offset(), revision: this.logRevision() }));
  protected readonly log = toSignal(toObservable(this.logQuery).pipe(switchMap(({ offset }) => this.api.errors(offset).pipe(
    map((value): DataState<JiraLogItem[]> => ({ kind: 'ready', value })),
    catchError((error: unknown) => of<DataState<JiraLogItem[]>>({ kind: 'error', message: errorMessage(error) })),
  ))), { initialValue: { kind: 'loading' } as DataState<JiraLogItem[]> });

  constructor() {
    this.form.controls.baseUrl.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.metadata.set(null));
    this.form.controls.project.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.metadata()?.statuses) {
        this.metadata.set(null);
      }
    });
    this.form.controls.replaceRest.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(replace => {
      if (!replace) { this.form.controls.restToken.reset(); }
    });
    this.form.controls.replaceWebhook.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(replace => {
      if (!replace) { this.form.controls.webhookSecret.reset(); }
    });
    this.form.controls.webhookSecret.addValidators(Validators.minLength(24));
    this.form.controls.webhookSecret.addValidators(Validators.maxLength(256));
  }

  protected addMapping(): void {
    this.form.controls.mappings.push(mapping('', ''));
  }

  protected refresh(): void {
    this.notice.set('');
    this.revision.update(value => value + 1);
  }

  protected page(step: number): void {
    this.offset.update(value => Math.max(0, value + step * jiraLogPageSize));
  }

  protected refreshLog(): void {
    this.logRevision.update(value => value + 1);
  }

  protected discover(projectDetails: boolean): void {
    if (this.busy()) {
      return;
    }
    const { baseUrl, project, restToken, replaceRest } = this.form.getRawValue();
    if (!jiraLink(baseUrl.trim()) || (projectDetails && !project.trim())) {
      this.discoveryError.set(jiraText.invalidData);
      return;
    }
    this.discoveryError.set('');
    this.busy.set(true);
    this.api.discover(baseUrl.trim(), !this.connected() || replaceRest ? restToken : '', projectDetails ? project.trim() : null).pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)),
    ).subscribe({
      next: metadata => this.metadata.set(metadata),
      error: (error: unknown) => this.discoveryError.set(this.message(error)),
    });
  }

  protected save(): void {
    if (this.busy() || this.conflicted()) {
      return;
    }
    const { baseUrl, project, issueType, doneStatus, holdStatus, cancelStatus, enabled, responsibleUserId,
      restToken, webhookSecret, replaceRest, replaceWebhook, mappings } = this.form.getRawValue();
    const sendRest = !this.connected() || replaceRest;
    const sendWebhook = !this.connected() || replaceWebhook;
    const ids = new Set(mappings.map(item => item.userId));
    const logins = new Set(mappings.map(item => item.login.trim().toLowerCase()));
    if (this.form.invalid || !jiraLink(baseUrl.trim()) || !project.trim() || !issueType.trim() || !doneStatus.trim()
      || !responsibleUserId || (sendRest && !restToken) || (sendWebhook && !webhookSecret) || ids.has('') || logins.has('') || ids.size !== mappings.length || logins.size !== mappings.length) {
      this.notice.set(jiraText.invalidData);
      return;
    }

    const body = {
      version: this.version, baseUrl: baseUrl.trim(), project: project.trim(), issueType: issueType.trim(),
      doneStatus: doneStatus.trim(), holdStatus: holdStatus.trim() || null, cancelStatus: cancelStatus.trim() || null,
      enabled, responsibleUserId, executors: Object.fromEntries(mappings.map(item => [item.userId, item.login.trim()])),
    };
    this.notice.set('');
    this.busy.set(true);
    // Секреты находятся в памяти формы до отправки. При повторной записи их нужно ввести снова.
    this.form.controls.restToken.reset();
    this.form.controls.webhookSecret.reset();
    this.api.save(body, sendRest ? restToken : '', sendWebhook ? webhookSecret : '').pipe(
      takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false)),
    ).subscribe({
      next: value => {
        this.populate(value);
        this.notice.set(jiraText.saved);
        this.refreshLog();
        this.api.settings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
          next: settings => this.panel.set(settings), error: error => this.panelNotice.set(errorMessage(error)),
        });
      },
      error: (error: unknown) => {
        const conflict = error instanceof HttpErrorResponse && error.status === 409;
        this.conflicted.set(conflict);
        this.notice.set(this.message(error));
      },
    });
  }

  protected userName(id: string | null, users: JiraUser[]): string {
    const user = users.find(entry => entry.userId === id);
    return user?.fullName || user?.email || id || text.unknown;
  }

  protected errorCode(value: string): string {
    // Журнал содержит коды СУР. Произвольный текст внешнего сервиса не показываем.
    return /^(?:JIRA|TREATMENT|RISK)_[A-Z_]+$/.test(value) || value === 'UNKNOWN' ? value : this.t.unknown;
  }

  private message(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      switch (problemCode(error)) {
        case 'JIRA_INVALID_CONFIGURATION': return jiraText.invalidData;
        case 'JIRA_UNAVAILABLE': return jiraText.unavailable;
        case 'JIRA_RECONCILIATION_CONFLICT': return jiraText.editBlocked;
      }
      if (error.status === 0 || error.status >= 500) {
        return text.unknownWrite;
      }
    }
    return errorMessage(error);
  }

  private populate(value: JiraSettings | null): void {
    this.connected.set(value !== null);
    this.panel.set(value);
    this.panelNotice.set('');
    this.conflicted.set(false);
    this.metadata.set(null);
    this.version = value?.version ?? initialVersion;
    this.zone.set(value?.timeZone ?? '');
    this.form.controls.mappings.clear();
    this.form.reset();
    if (value) {
      this.form.patchValue({ ...value, holdStatus: value.holdStatus ?? '', cancelStatus: value.cancelStatus ?? '' });
      for (const [userId, login] of Object.entries(value.executors)) {
        this.form.controls.mappings.push(mapping(userId, login));
      }
    }
  }
}

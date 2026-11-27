import { HttpClient, HttpContext, HttpContextToken, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Observable, Subject, defer, finalize, firstValueFrom, shareReplay } from 'rxjs';
import { api, browserAuth, isPasswordChangeRequired } from '../http/api';
import { readSessionClaims, withSessionLock } from './session-policy';

export interface Credentials {
  email: string;
  password: string;
}

interface AccessResponse { accessToken: string }
interface CsrfResponse { requestToken: string | null }
interface CurrentAccess { userId: string; rightCodes: string[] }
interface PasswordResponse { isSuccess: boolean }

const changePasswordEndpoint = `${api}/users/change-password`;
export const sessionRequest = new HttpContextToken(() => false);
const currentUserEndpoint = `${api}/users/me`;
const logoutSignal = 'logout';
const channelName = 'sur:session';
const pendingLogoutKey = 'sur:logout-unconfirmed';

interface SessionState {
  token: string;
  sid: string;
  restricted: boolean;
  profile: CurrentAccess | null;
}

/** Держит JWT и снимок прав в памяти вкладки. Cookie целиком управляется сервером. */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly state = signal<SessionState | null>(null);
  private readonly ended = new Subject<'expired' | 'unavailable' | 'logout' | 'password-changed'>();
  private readonly channel = typeof BroadcastChannel === 'function' ? new BroadcastChannel(channelName) : null;
  private readonly uncertainLogout = signal(this.hasPendingLogout());
  private epoch = 0;
  private notifiedEpoch = -1;
  private restoreBlocked = false;
  private restoring?: Observable<void>;
  private loadingProfile?: { token: string; request: Observable<void> };

  readonly token = computed(() => this.state()?.token ?? null);
  readonly sid = computed(() => this.state()?.sid ?? null);
  readonly restricted = computed(() => this.state()?.restricted ?? false);
  readonly userId = computed(() => this.state()?.profile?.userId ?? null);
  /** Проверка видимости действия, сервер повторно проверяет права и назначение. */
  hasRight(right: string): boolean {
    const rights = this.rights();
    return rights.includes('admin') || rights.includes(right);
  }

  readonly rights = computed<readonly string[]>(() => this.state()?.profile?.rightCodes ?? []);
  readonly logoutUnconfirmed = this.uncertainLogout.asReadonly();
  readonly ended$ = this.ended.asObservable();

  constructor() {
    const onStorage = (event: StorageEvent) => {
      if (event.key === pendingLogoutKey || event.key === null) {
        this.uncertainLogout.set(this.hasPendingLogout());
      }
    };
    window.addEventListener('storage', onStorage);
    this.channel?.addEventListener('message', event => {
      if (event.data === logoutSignal) {
        this.uncertainLogout.set(this.hasPendingLogout());
        this.clear(true);
        this.ended.next('logout');
      }
    });
    inject(DestroyRef).onDestroy(() => {
      window.removeEventListener('storage', onStorage);
      this.channel?.close();
    });
  }

  login(credentials: Credentials): Observable<void> {
    return defer(() => {
      // Новый вход отделён от незавершённых чтений и ротации предыдущей сессии.
      this.clear(true);
      const epoch = this.epoch;
      return withSessionLock(navigator.locks, async () => {
        this.ensureCurrent(epoch);
        const response = await this.post<AccessResponse>('token', credentials);
        await this.accept(response, epoch);
        this.restoreBlocked = false;
        this.setPendingLogout(false);
      });
    });
  }

  restore(): Observable<void> {
    if (this.restoring) {
      return this.restoring;
    }

    const epoch = this.epoch;
    const previousSid = this.sid();
    this.restoring = defer(() => withSessionLock(navigator.locks, async () => {
      this.ensureCurrent(epoch);
      if (this.restoreBlocked || this.hasPendingLogout()) {
        throw new HttpErrorResponse({ status: 401 });
      }

      try {
        const response = await this.post<AccessResponse>('token/refresh', {});
        await this.accept(response, epoch, previousSid);
      } catch (error) {
        if (epoch === this.epoch) {
          // Если результат ротации не подтверждён, следующий запрос может повторить старую cookie.
          const expired = error instanceof HttpErrorResponse && error.status === 401;
          this.clear(!expired);
        }
        throw error;
      }
    })).pipe(
      finalize(() => { this.restoring = undefined; }),
      // Уход со страницы не должен прерывать ротацию после отправки запроса.
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.restoring;
  }

  reloadAccess(): Observable<void> {
    const current = this.state();
    if (!current || current.restricted) {
      return defer(() => Promise.resolve());
    }
    if (this.loadingProfile?.token === current.token) {
      return this.loadingProfile.request;
    }

    const request = defer(async () => {
      try {
        this.applyProfile(current, await this.getProfile(current.token));
      } catch (error) {
        if (this.state() === current) {
          this.applyProfile(current, null, isPasswordChangeRequired(error));
        }
        throw error;
      }
    }).pipe(
      finalize(() => {
        if (this.loadingProfile?.request === request) {
          this.loadingProfile = undefined;
        }
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.loadingProfile = { token: current.token, request };
    return request;
  }

  changePassword(oldPassword: string, newPassword: string): Observable<boolean> {
    return defer(async () => {
      const token = this.token();
      const epoch = this.epoch;
      if (!token) {
        throw new HttpErrorResponse({ status: 401 });
      }

      return withSessionLock(navigator.locks, async () => {
        this.ensureCurrent(epoch);
        const result = await firstValueFrom(this.http.patch<PasswordResponse>(changePasswordEndpoint,
          { oldPassword, newPassword }, this.authorize(token)));
        if (result.isSuccess) {
          // PATCH отзывает сеансы этой учётной записи. Cookie могла быть заменена входом в другой вкладке.
          this.endEverywhere('password-changed');
        }
        return result.isSuccess;
      });
    });
  }

  logout(): Observable<void> {
    return defer(() => {
      this.setPendingLogout(true);
      this.endEverywhere('logout');
      return withSessionLock(navigator.locks, () => this.revokeCookie()).catch(() => undefined);
    });
  }

  expire(expectedToken: string): void {
    if (this.token() === expectedToken) {
      this.clear(false);
      this.ended.next('expired');
    }
  }

  notifyRestoreFailure(unavailable: boolean): void {
    if (!this.token() && this.notifiedEpoch !== this.epoch) {
      this.notifiedEpoch = this.epoch;
      this.ended.next(unavailable ? 'unavailable' : 'expired');
    }
  }

  private async accept(response: AccessResponse, epoch: number, expectedSid: string | null = null): Promise<void> {
    const claims = readSessionClaims(response.accessToken);
    if (expectedSid && claims.sid !== expectedSid) {
      // Другая вкладка могла выполнить вход другим аккаунтом. Старые запросы продолжать нельзя.
      throw new HttpErrorResponse({ status: 401 });
    }
    let profile: CurrentAccess | null = null;
    let restricted = claims.passwordChangeRequired;
    try {
      profile = await this.getProfile(response.accessToken);
    } catch (error) {
      if (!isPasswordChangeRequired(error) && !(restricted && error instanceof HttpErrorResponse && error.status === 403)) {
        throw error;
      }
      restricted = true;
    }
    this.ensureCurrent(epoch);
    this.state.set({ token: response.accessToken, sid: claims.sid, restricted, profile: restricted ? null : profile });
  }

  private applyProfile(current: SessionState, profile: CurrentAccess | null, restricted = false): void {
    if (this.state() === current) {
      this.state.set({ ...current, profile, restricted });
    }
  }

  private async getProfile(token: string): Promise<CurrentAccess> {
    const profile = await firstValueFrom(this.http.get<CurrentAccess>(currentUserEndpoint, this.authorize(token)));
    if (!profile || typeof profile.userId !== 'string' || !Array.isArray(profile.rightCodes)
      || !profile.rightCodes.every(code => typeof code === 'string')) {
      throw new Error('Invalid current user response');
    }
    return profile;
  }

  private authorize(token: string): { context: HttpContext; headers: HttpHeaders } {
    return {
      context: new HttpContext().set(sessionRequest, true),
      headers: new HttpHeaders({ Authorization: `Bearer ${token}` }),
    };
  }

  private async post<T>(path: string, body: object): Promise<T> {
    const { requestToken } = await firstValueFrom(this.http.get<CsrfResponse>(`${browserAuth}/csrf`));
    if (!requestToken) {
      throw new Error('CSRF token missing');
    }
    return firstValueFrom(this.http.post<T>(`${browserAuth}/${path}`, body, {
      headers: new HttpHeaders({ 'X-CSRF-TOKEN': requestToken }),
    }));
  }

  private async revokeCookie(): Promise<void> {
    try {
      await this.post<void>('token/revoke', {});
      this.setPendingLogout(false);
    } catch {
      this.setPendingLogout(true);
    }
  }

  private hasPendingLogout(): boolean {
    try {
      return localStorage.getItem(pendingLogoutKey) === '1';
    } catch {
      return false;
    }
  }

  /** Храним только флаг незавершённого отзыва, чтобы reload не восстановил эту cookie. */
  private setPendingLogout(pending: boolean): void {
    this.uncertainLogout.set(pending);
    try {
      if (pending) {
        localStorage.setItem(pendingLogoutKey, '1');
      } else {
        localStorage.removeItem(pendingLogoutKey);
      }
    } catch {
      // При отключённом хранилище флаг доступен до закрытия страницы.
    }
  }

  private endEverywhere(reason: 'logout' | 'password-changed'): void {
    this.clear(true);
    this.channel?.postMessage(logoutSignal);
    this.ended.next(reason);
  }

  private clear(blockRestore: boolean): void {
    this.epoch++;
    this.restoreBlocked = blockRestore;
    this.state.set(null);
  }

  private ensureCurrent(epoch: number): void {
    if (epoch !== this.epoch) {
      throw new HttpErrorResponse({ status: 401 });
    }
  }
}

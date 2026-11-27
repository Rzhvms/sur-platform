/** Характеристики JWT для навигации и сопоставления двух версий одной сессии. Права проверяет API. */
export interface SessionClaims {
  sid: string;
  passwordChangeRequired: boolean;
}

export class UnsupportedSessionBrowser extends Error {}

/** Сериализует всю операцию с cookie, включая получение привязанного к ней CSRF. */
export function withSessionLock<T>(locks: LockManager | undefined, action: () => Promise<T>): Promise<T> {
  if (!locks) {
    // shortcut: вход отключён в браузерах без Web Locks до согласования безопасного восстановления.
    return Promise.reject(new UnsupportedSessionBrowser());
  }
  return locks.request('sur:browser-session', action);
}

export function readSessionClaims(token: string): SessionClaims {
  const parts = token.split('.');
  if (parts.length !== 3) {
    throw new Error('Invalid access token');
  }
  const payload: unknown = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')));
  if (!payload || typeof payload !== 'object' || !('sid' in payload)) {
    throw new Error('Missing session ID');
  }
  const sid = payload.sid;
  if (typeof sid !== 'string' || !sid) {
    throw new Error('Missing session ID');
  }
  const restricted = 'password_change_required' in payload && payload.password_change_required;
  return { sid, passwordChangeRequired: restricted === true || restricted === 'true' };
}

/** Возвращает только обычные внутренние маршруты приложения. */
export function internalDestination(value: string | null): string | null {
  if (!value || !/^\/(risks|treatments|approvals|admin|administration|documents|reports|imports|help)(?:\/|[?#]|$)/.test(value)) {
    return null;
  }
  const path = value.split(/[?#]/, 1)[0];
  if (/[\\\x00-\x20]/.test(value) || /%(?:2e|2f|5c)|(?:^|\/)\.{1,2}(?:\/|$)|\/\//i.test(path)) {
    return null;
  }
  return value;
}

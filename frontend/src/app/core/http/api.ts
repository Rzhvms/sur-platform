import { HttpErrorResponse } from '@angular/common/http';
import { UnsupportedSessionBrowser } from '../auth/session-policy';
import { text } from '../../shared/text';

export const api = '/api/v1/public';
export const providerApi = '/api/v1/provider';
export const browserAuth = `${api}/connect/browser`;

export const passwordChangeRequired = 'password_change_required';

export function problemCode(error: HttpErrorResponse): string | undefined {
  const body: unknown = error.error;
  if (body && typeof body === 'object' && 'code' in body && typeof body.code === 'string') {
    return body.code;
  }
  return undefined;
}

export function isPasswordChangeRequired(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 403 && problemCode(error) === passwordChangeRequired;
}

export function errorMessage(error: unknown): string {
  if (error instanceof UnsupportedSessionBrowser) {
    return text.unsupportedBrowser;
  }
  if (isPasswordChangeRequired(error)) {
    return text.passwordChangeRequired;
  }
  if (error instanceof HttpErrorResponse && problemCode(error) === 'STALE_VERSION') {
    return text.stale;
  }
  if (!(error instanceof HttpErrorResponse) || error.status === 0 || error.status >= 500) {
    return text.unavailable;
  }
  switch (error.status) {
    case 401: return text.authRequired;
    case 403: return text.forbidden;
    case 404: return text.missing;
    case 409: return text.stale;
    default: return text.invalid;
  }
}

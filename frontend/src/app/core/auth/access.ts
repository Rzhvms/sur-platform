import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, switchMap, throwError } from 'rxjs';
import { approvalRights, treatmentText } from '../../features/treatments/treatment-contract';
import { documentRights, documentText } from '../../features/documents/document-contract';
import { text } from '../../shared/text';
import { api, browserAuth, isPasswordChangeRequired, providerApi } from '../http/api';
import { SessionService, sessionRequest } from './session.service';

export const sections = [
  { path: 'risks', label: text.risks, rights: ['view_risk'] },
  { path: 'treatments', label: text.treatments, rights: ['view_risk'] },
  { path: 'approvals', label: treatmentText.approvals, rights: approvalRights },
  { path: 'reports', label: documentText.reports, rights: [documentRights.view] },
  { path: 'imports', label: documentText.imports, rights: [documentRights.import] },
  { path: 'admin', label: text.administration, rights: ['admin'] },
] as const;

/** Каркас проверяет вход в раздел. Назначения и права конкретных команд проверяет API. */
export function canOpen(rights: readonly string[], section: (typeof sections)[number]): boolean {
  return rights.includes('admin') || section.rights.some(right => rights.includes(right));
}

export function homeFor(rights: readonly string[]): string {
  const first = sections.find(section => canOpen(rights, section));
  return first ? `/${first.path}` : '/forbidden';
}

export const requireSession: CanActivateFn = (route, state) => {
  const session = inject(SessionService);
  const router = inject(Router);
  const passwordRoute = route.routeConfig?.path === 'change-password';
  const target = passwordRoute ? null : state.url;

  const decide = () => {
    if (session.restricted()) {
      return passwordRoute || router.createUrlTree(['/change-password']);
    }
    if (passwordRoute) {
      return router.parseUrl(homeFor(session.rights()));
    }
    const section = sections.find(item => item.path === route.routeConfig?.path);
    return !section || canOpen(session.rights(), section) || router.createUrlTree(['/forbidden']);
  };

  if (session.token()) {
    return decide();
  }
  return session.restore().pipe(
    map(decide),
    catchError((error: unknown) => of(router.createUrlTree(['/login'], {
      queryParams: { next: target, restore: error instanceof HttpErrorResponse && error.status === 401 ? null : 'failed' },
    }))),
  );
};

export const accessToken: HttpInterceptorFn = (request, next) => {
  const browserEndpoint = request.url.startsWith(`${api}/`) || request.url.startsWith(`${providerApi}/`);
  if (!browserEndpoint || request.url.startsWith(`${browserAuth}/`) || request.context.get(sessionRequest)) {
    return next(request);
  }
  const session = inject(SessionService);
  const token = session.token();
  const sid = session.sid();
  if (!token) {
    return next(request);
  }
  const authorize = (access: string) => request.clone({ setHeaders: { Authorization: `Bearer ${access}` } });
  return next(authorize(token)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || session.sid() !== sid) {
        return throwError(() => error);
      }
      if (error.status === 403) {
        // Обновляем навигацию и права, сохраняя 403 исходной операции для экрана.
        return session.reloadAccess().pipe(
          catchError((profileError: unknown) => {
            if (profileError instanceof HttpErrorResponse && profileError.status === 401) {
              return session.restore().pipe(catchError((refreshError: unknown) => {
                session.notifyRestoreFailure(!(refreshError instanceof HttpErrorResponse && refreshError.status === 401));
                return of(undefined);
              }));
            }
            return of(undefined);
          }),
          switchMap(() => throwError(() => error)),
        );
      }
      if (error.status !== 401) {
        return throwError(() => error);
      }

      const current = session.token();
      const refresh = current && current !== token ? of(undefined) : session.restore().pipe(
        catchError((refreshError: unknown) => {
          session.notifyRestoreFailure(!(refreshError instanceof HttpErrorResponse && refreshError.status === 401));
          return throwError(() => refreshError);
        }),
      );
      return refresh.pipe(switchMap(() => {
        const renewed = session.token();
        // Изменяющие команды не повторяются. При смене аккаунта чтение также не повторяется.
        if (!renewed || session.sid() !== sid || (request.method !== 'GET' && request.method !== 'HEAD')) {
          return throwError(() => error);
        }
        return next(authorize(renewed)).pipe(catchError((retryError: unknown) => {
          if (retryError instanceof HttpErrorResponse && retryError.status === 401) {
            session.expire(renewed);
          }
          if (isPasswordChangeRequired(retryError)) {
            return session.reloadAccess().pipe(
              catchError(() => of(undefined)),
              switchMap(() => throwError(() => retryError)),
            );
          }
          return throwError(() => retryError);
        }));
      }));
    }),
  );
};

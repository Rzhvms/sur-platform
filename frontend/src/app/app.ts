import { ChangeDetectionStrategy, Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet, Routes } from '@angular/router';
import { filter, finalize } from 'rxjs';
import { canOpen, requireSession, sections } from './core/auth/access';
import { ChangePassword } from './core/auth/change-password';
import { Forbidden } from './core/auth/forbidden';
import { Login } from './core/auth/login';
import { Session } from './core/auth/session';
import { internalDestination } from './core/auth/session-policy';
import { treatmentText } from './features/treatments/treatment-contract';
import { text } from './shared/text';
import { documentText } from './features/documents/document-contract';

@Component({
  selector: 'sur-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="skip" href="#main" (click)="skipToMain($event, main)">{{ t.skip }}</a>
    @if (!login()) {
      <header class="topbar">
        <a class="brand" [routerLink]="demo() ? '/demo/risks' : '/risks'" [attr.aria-label]="t.product">
          <span class="brand-mark" aria-hidden="true">{{ t.brand }}</span>
        </a>
        <nav [attr.aria-label]="t.navigation">
          @if (demo()) {
            <a routerLink="/demo/risks" routerLinkActive="selected" ariaCurrentWhenActive="page">{{ t.risks }}</a>
          } @else {
            @for (section of visibleSections(); track section.path) {
              <a [routerLink]="['/', section.path]" routerLinkActive="selected" ariaCurrentWhenActive="page">{{ section.label }}</a>
            }
          }
        </nav>
        @if (demo()) {
          <span class="demo-label">{{ t.demo }}</span>
          <a class="session-action" routerLink="/login">{{ t.login }}</a>
        } @else if (session.token()) {
          <button class="session-action" [disabled]="leaving()" (click)="logout()">{{ t.logout }}</button>
        }
      </header>
    }
    <main #main id="main" tabindex="-1" [class.login-layout]="login()">
      <router-outlet />
    </main>
  `,
})
export class App {
  protected readonly t = text;
  protected readonly session = inject(Session);
  protected readonly demo = signal(false);
  protected readonly login = signal(false);
  protected readonly leaving = signal(false);
  protected readonly visibleSections = computed(() => sections.filter(section => canOpen(this.session.rights(), section)));
  private readonly location = signal('');
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(event => {
      this.demo.set(event.urlAfterRedirects.startsWith('/demo/'));
      this.location.set(event.urlAfterRedirects);
      this.login.set(/^\/(login|change-password)(?:[?#]|$)/.test(event.urlAfterRedirects));
    });
    this.session.ended$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(reason => {
      const url = this.location();
      if (!/^\/login(?:[?#]|$)/.test(url) && !this.demo()) {
        const next = reason === 'password-changed' ? null : internalDestination(url);
        void this.router.navigate(['/login'], {
          queryParams: { next, changed: reason === 'password-changed' ? '1' : null, restore: reason === 'unavailable' ? 'failed' : null },
          replaceUrl: true,
        });
      }
    });

    // Изменение прав после 403 или ротации исключает уже открытый раздел из доступных.
    effect(() => {
      const url = this.location();
      if (!this.session.token()) {
        return;
      }
      const section = sections.find(item => url.split(/[/?#]/)[1] === item.path);
      if (this.session.restricted() && (section || url.startsWith('/forbidden'))) {
        void this.router.navigateByUrl('/change-password', { replaceUrl: true });
      } else if (section && !canOpen(this.session.rights(), section)) {
        void this.router.navigateByUrl('/forbidden', { replaceUrl: true });
      }
    });
  }

  protected skipToMain(event: Event, main: HTMLElement): void {
    event.preventDefault();
    main.focus();
  }

  protected logout(): void {
    this.leaving.set(true);
    this.session.logout().pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.leaving.set(false)),
    ).subscribe();
  }
}

@Component({
  imports: [RouterLink],
  template: `<h1>{{ t.notFound }}</h1><a routerLink="/risks">{{ t.back }}</a>`,
})
class NotFound {
  protected readonly t = text;
}

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'risks' },
  { path: 'login', component: Login, title: text.login },
  { path: 'change-password', component: ChangePassword, canActivate: [requireSession], title: text.changePassword },
  { path: 'forbidden', component: Forbidden, canActivate: [requireSession], title: text.accessDenied },
  { path: 'risks', canActivate: [requireSession], loadChildren: () => import('./features/risks/routes').then(m => m.riskRoutes) },
  { path: 'demo/risks', data: { demo: true }, loadChildren: () => import('./features/risks/routes').then(m => m.demoRiskRoutes) },
  { path: 'demo', pathMatch: 'full', redirectTo: 'demo/risks' },
  {
    path: 'treatments', canActivate: [requireSession], children: [
      { path: '', title: text.treatments, loadComponent: () => import('./features/treatments/page').then(m => m.Treatments) },
      { path: ':id', title: treatmentText.treatment, loadComponent: () => import('./features/treatments/treatment-detail').then(m => m.TreatmentDetail) },
    ],
  },
  { path: 'approvals', canActivate: [requireSession], title: treatmentText.approvals, loadComponent: () => import('./features/treatments/approvals').then(m => m.Approvals) },
  { path: 'admin', canActivate: [requireSession], loadChildren: () => import('./features/administration/routes').then(m => m.adminRoutes) },
  { path: 'administration', pathMatch: 'full', redirectTo: 'admin' },
  { path: 'administration/jira', pathMatch: 'full', redirectTo: 'admin/jira' },
  { path: 'reports', canActivate: [requireSession], title: documentText.reports, loadComponent: () => import('./features/documents/reports').then(m => m.Reports) },
  { path: 'imports', canActivate: [requireSession], title: documentText.imports, loadComponent: () => import('./features/documents/imports').then(m => m.Imports) },
  { path: 'documents', pathMatch: 'full', redirectTo: 'reports' },
  { path: '**', component: NotFound, title: text.notFound },
];

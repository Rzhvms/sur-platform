import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
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
import { UiTiming } from './core/release/ui-timing';
import { Release } from './core/release/release';
import { releaseText } from './core/release/release-policy';
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
        <button class="menu-toggle" type="button" [attr.aria-label]="t.openMenu"
          aria-haspopup="dialog" aria-controls="navigation-menu" [attr.aria-expanded]="menuOpen()" (click)="openMenu()">
          <svg aria-hidden="true" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round">
            <path d="M4 6h16M4 12h16M4 18h16" />
          </svg>
        </button>
      </header>
      <dialog #navigationMenu id="navigation-menu" class="navigation-drawer" aria-labelledby="navigation-title"
        (close)="menuOpen.set(false)" (click)="dismissBackdrop($event)" (keydown)="keepMenuFocus($event)">
        <div class="drawer-heading">
          <h2 id="navigation-title">{{ t.menu }}</h2>
          <button class="drawer-close" type="button" [attr.aria-label]="t.closeMenu" (click)="closeMenu()" autofocus>
            <svg aria-hidden="true" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round">
              <path d="m6 6 12 12M18 6 6 18" />
            </svg>
          </button>
        </div>
        <nav class="drawer-navigation" [attr.aria-label]="t.navigation">
          @if (demo()) {
            <a routerLink="/demo/risks" routerLinkActive="selected" ariaCurrentWhenActive="page" (click)="closeMenu()">{{ t.risks }}</a>
          } @else {
            @for (section of visibleSections(); track section.path) {
              <a [routerLink]="['/', section.path]" routerLinkActive="selected" ariaCurrentWhenActive="page" (click)="closeMenu()">{{ section.label }}</a>
            }
            @if (session.token() && !session.restricted()) {
              <a routerLink="/help" routerLinkActive="selected" ariaCurrentWhenActive="page" (click)="closeMenu()">{{ t.help }}</a>
            }
          }
        </nav>
        <div class="drawer-footer">
          @if (demo()) {
            <span class="demo-label">{{ t.demo }}</span>
            <a class="session-action" routerLink="/login" (click)="closeMenu()">{{ t.login }}</a>
          } @else if (session.token()) {
            <button class="session-action" [disabled]="leaving()" (click)="logout()">{{ t.logout }}</button>
          }
        </div>
      </dialog>
    }
    <main #main id="main" tabindex="-1" [class.login-layout]="login() || (!demo() && !release.ready())">
      @if (demo() || release.ready()) {
        <router-outlet />
      } @else {
        <section class="release-card" role="status" aria-live="polite">
          <div class="brand-mark release-mark" aria-hidden="true">{{ t.brand }}</div>
          @if (release.busy() && !release.message()) {
            <span class="spinner" aria-hidden="true"></span>
            <p class="muted">{{ t.loading }}</p>
          } @else {
            <h1>{{ r.heading }}</h1>
            <p>{{ release.message() }}</p>
            <div class="release-actions">
              <button class="primary" type="button" [disabled]="release.busy()" (click)="release.check()">{{ r.retry }}</button>
              <button class="release-secondary" type="button" (click)="reloadApplication()">{{ r.refresh }}</button>
            </div>
          }
        </section>
      }
    </main>
  `,
})
export class App {
  protected readonly t = text;
  protected readonly r = releaseText;
  protected readonly release = inject(Release);
  protected readonly session = inject(Session);
  protected readonly demo = signal(false);
  protected readonly login = signal(false);
  protected readonly leaving = signal(false);
  protected readonly menuOpen = signal(false);
  private readonly navigationMenu = viewChild<ElementRef<HTMLDialogElement>>('navigationMenu');
  protected readonly visibleSections = computed(() => sections.filter(section => canOpen(this.session.rights(), section)));
  private readonly timing = inject(UiTiming);
  private readonly location = signal('');
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.release.check();
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(event => {
      this.closeMenu();
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

  /** Native dialog удерживает фокус и возвращает его на бургер при закрытии */
  protected openMenu(): void {
    const menu = this.navigationMenu()?.nativeElement;
    if (menu && !menu.open) {
      menu.showModal();
      this.menuOpen.set(true);
    }
  }

  /** Закрывает панель при выборе раздела, выходе или изменении маршрута */
  protected closeMenu(): void {
    this.navigationMenu()?.nativeElement.close();
    this.menuOpen.set(false);
  }

  /** Замыкает Tab между крайними элементами панели, не затрагивая Escape */
  protected keepMenuFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') { return; }
    const items = this.navigationMenu()?.nativeElement.querySelectorAll<HTMLElement>('a[href], button:not(:disabled)');
    if (!items?.length) { return; }
    const first = items[0];
    const last = items[items.length - 1];
    if (event.shiftKey && event.target === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && event.target === last) {
      event.preventDefault();
      first.focus();
    }
  }

  /** Клик по backdrop попадает в dialog, но лежит за границами самой панели */
  protected dismissBackdrop(event: MouseEvent): void {
    const menu = this.navigationMenu()?.nativeElement;
    if (!menu || event.target !== menu) { return; }
    const bounds = menu.getBoundingClientRect();
    if (event.clientX < bounds.left || event.clientX > bounds.right ||
        event.clientY < bounds.top || event.clientY > bounds.bottom) {
      this.closeMenu();
    }
  }

  protected reloadApplication(): void {
    window.location.reload();
  }

  protected skipToMain(event: Event, main: HTMLElement): void {
    event.preventDefault();
    main.focus();
  }

  protected logout(): void {
    this.closeMenu();
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
  { path: 'help', canActivate: [requireSession], title: text.help, loadComponent: () => import('./features/help/help').then(m => m.Help) },
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

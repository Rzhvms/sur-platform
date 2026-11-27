import { Component, DestroyRef, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, finalize } from 'rxjs';
import { canOpen, sections } from './core/auth/access';
import { SessionService } from './core/auth/session.service';
import { internalDestination } from './core/auth/session-policy';
import { UiTimingService } from './core/release/ui-timing.service';
import { text } from './shared/text';

@Component({
  selector: 'sur-root',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
})
export class AppComponent {
  protected readonly t = text;
  protected readonly session = inject(SessionService);
  protected readonly demo = signal(false);
  protected readonly login = signal(false);
  protected readonly leaving = signal(false);
  protected readonly menuOpen = signal(false);
  private readonly navigationMenu = viewChild<ElementRef<HTMLDialogElement>>('navigationMenu');
  protected readonly visibleSections = computed(() => sections.filter(section => canOpen(this.session.rights(), section)));
  private readonly timing = inject(UiTimingService);
  private readonly location = signal('');
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
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

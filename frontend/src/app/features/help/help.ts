import { DOCUMENT } from '@angular/common';
import { ChangeDetectionStrategy, Component, Injector, afterNextRender, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Session } from '../../core/auth/session';
import { helpContent } from './help-content';

/** Показывает справку и права текущей сессии без административных запросов */
@Component({
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './help.html',
})
export class Help {
  private readonly session = inject(Session);
  private readonly document = inject(DOCUMENT);
  private readonly injector = inject(Injector);
  protected readonly content = helpContent;
  protected readonly rights = computed(() => [...this.session.rights()].sort());

  constructor() {
    // Фокус следует за фрагментом и при прямой ссылке, и при навигации назад/вперёд.
    inject(ActivatedRoute).fragment.pipe(takeUntilDestroyed()).subscribe(fragment => {
      afterNextRender(() => this.focusFragment(fragment), { injector: this.injector });
    });
  }

  private focusFragment(fragment: string | null): void {
    if (fragment) {
      this.document.getElementById(fragment)?.focus({ preventScroll: true });
    }
  }
}

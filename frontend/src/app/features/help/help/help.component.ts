import { DOCUMENT } from '@angular/common';
import { Component, Injector, afterNextRender, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SessionService } from '../../../core/auth/session.service';
import { helpContent } from '../help-content';

/** Показывает справку и права текущей сессии без административных запросов */
@Component({
  imports: [RouterLink],
  templateUrl: './help.component.html',
})
export class HelpComponent {
  private readonly session = inject(SessionService);
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

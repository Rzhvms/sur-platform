import { HttpClient } from '@angular/common/http';
import { Injectable, inject, isDevMode, signal } from '@angular/core';
import { finalize, forkJoin, map, of } from 'rxjs';
import { SpaRelease, compatibleRelease, releaseText, sameRelease } from './release-policy';

/** Проверяет доступный API до открытия пользовательских форм. */
@Injectable({ providedIn: 'root' })
export class ReleaseService {
  private readonly http = inject(HttpClient);
  readonly ready = signal(false);
  readonly message = signal('');
  readonly busy = signal(false);

  check(): void {
    if (this.busy()) { return; }
    this.ready.set(false);
    this.message.set('');
    this.busy.set(true);
    forkJoin({
      api: this.http.get<unknown>('/health/release'),
      spa: isDevMode() ? of(null) : this.http.get<SpaRelease>('/release.json'),
    }).pipe(
      map(({ api, spa }) => spa ? sameRelease(api, spa) : compatibleRelease(api)),
      finalize(() => this.busy.set(false)),
    ).subscribe({
      next: value => {
        this.ready.set(value);
        if (!this.ready()) { this.message.set(releaseText.incompatible); }
      },
      error: () => this.message.set(releaseText.unavailable),
    });
  }
}

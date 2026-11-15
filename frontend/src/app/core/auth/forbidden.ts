import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { text } from '../../shared/text';
import { homeFor } from './access';
import { Session } from './session';

@Component({
  selector: 'sur-forbidden',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="login-card">
      <h1>{{ t.accessDenied }}</h1>
      <p class="muted">{{ t.forbidden }}</p>
      @if (home() !== '/forbidden') { <a [routerLink]="home()">{{ t.returnToSection }}</a> }
    </section>
  `,
})
export class Forbidden {
  protected readonly t = text;
  private readonly session = inject(Session);
  protected readonly home = computed(() => homeFor(this.session.rights()));
}

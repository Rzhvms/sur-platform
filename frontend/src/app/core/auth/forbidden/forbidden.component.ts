import { Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { text } from '../../../shared/text';
import { homeFor } from '../access';
import { SessionService } from '../session.service';

@Component({
  selector: 'sur-forbidden',
  imports: [RouterLink],
  templateUrl: './forbidden.component.html',
})
export class ForbiddenComponent {
  protected readonly t = text;
  private readonly session = inject(SessionService);
  protected readonly home = computed(() => homeFor(this.session.rights()));
}

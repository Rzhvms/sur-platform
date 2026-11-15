import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { text } from '../../shared/text';
import { documentText } from '../documents/document-contract';
import { adminText } from './admin-api';

@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="admin-navigation" [attr.aria-label]="t.administration">
      @for (section of sections; track section.path) {
        <a [routerLink]="section.path" routerLinkActive="selected" ariaCurrentWhenActive="page">{{ section.label }}</a>
      }
    </nav>
    <router-outlet />
  `,
})
export class Administration {
  protected readonly t = text;
  protected readonly sections = [
    { path: 'users', label: adminText.users }, { path: 'roles', label: adminText.roles },
    { path: 'organization', label: adminText.organization }, { path: 'risk-settings', label: adminText.riskSettings },
    { path: 'reviews', label: adminText.reviews }, { path: 'jira', label: adminText.jira }, { path: 'history', label: documentText.history },
  ];
}

import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { text } from '../../../shared/text';
import { documentText } from '../../documents/document-contract';
import { adminText } from '../admin-api.service';

@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './administration-page.component.html',
})
export class AdministrationPageComponent {
  protected readonly t = text;
  protected readonly sections = [
    { path: 'users', label: adminText.users }, { path: 'roles', label: adminText.roles },
    { path: 'organization', label: adminText.organization }, { path: 'risk-settings', label: adminText.riskSettings },
    { path: 'reviews', label: adminText.reviews }, { path: 'jira', label: adminText.jira }, { path: 'history', label: documentText.history },
  ];
}

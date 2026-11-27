import { Routes } from '@angular/router';
import { documentText } from '../documents/document-contract';
import { adminText } from './admin-api.service';

export const adminRoutes: Routes = [
  { path: '', loadComponent: () => import('./administration-page/administration-page.component').then(m => m.AdministrationPageComponent), children: [
    { path: '', pathMatch: 'full', redirectTo: 'users' },
    { path: 'users', title: adminText.users, loadComponent: () => import('./admin-users/admin-users.component').then(m => m.AdminUsersComponent) },
    { path: 'users/new', title: adminText.users, loadComponent: () => import('./admin-user-card/admin-user-card.component').then(m => m.AdminUserCardComponent) },
    { path: 'users/:id', title: adminText.users, loadComponent: () => import('./admin-user-card/admin-user-card.component').then(m => m.AdminUserCardComponent) },
    { path: 'roles', title: adminText.roles, loadComponent: () => import('./admin-roles/admin-roles.component').then(m => m.AdminRolesComponent) },
    { path: 'organization', title: adminText.organization, loadComponent: () => import('./admin-organization/admin-organization.component').then(m => m.AdminOrganizationComponent) },
    { path: 'risk-settings', title: adminText.riskSettings, loadComponent: () => import('./admin-risk-settings/admin-risk-settings.component').then(m => m.AdminRiskSettingsComponent) },
    { path: 'reviews', title: adminText.reviews, loadComponent: () => import('./admin-reviews/admin-reviews.component').then(m => m.AdminReviewsComponent) },
    { path: 'history', title: documentText.history, loadComponent: () => import('../documents/admin-history/admin-history.component').then(m => m.AdminHistoryComponent) },
    { path: 'jira', title: adminText.jira, loadComponent: () => import('./jira-settings-page/jira-settings-page.component').then(m => m.JiraSettingsPageComponent) },
  ] },
];

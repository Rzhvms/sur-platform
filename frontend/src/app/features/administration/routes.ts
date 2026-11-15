import { Routes } from '@angular/router';
import { documentText } from '../documents/document-contract';
import { adminText } from './admin-api';

export const adminRoutes: Routes = [
  { path: '', loadComponent: () => import('./page').then(m => m.Administration), children: [
    { path: '', pathMatch: 'full', redirectTo: 'users' },
    { path: 'users', title: adminText.users, loadComponent: () => import('./users').then(m => m.AdminUsers) },
    { path: 'users/new', title: adminText.users, loadComponent: () => import('./user-card').then(m => m.AdminUserCard) },
    { path: 'users/:id', title: adminText.users, loadComponent: () => import('./user-card').then(m => m.AdminUserCard) },
    { path: 'roles', title: adminText.roles, loadComponent: () => import('./roles').then(m => m.AdminRoles) },
    { path: 'organization', title: adminText.organization, loadComponent: () => import('./organization').then(m => m.AdminOrganization) },
    { path: 'risk-settings', title: adminText.riskSettings, loadComponent: () => import('./risk-settings').then(m => m.AdminRiskSettings) },
    { path: 'reviews', title: adminText.reviews, loadComponent: () => import('./reviews').then(m => m.AdminReviews) },
    { path: 'history', title: documentText.history, loadComponent: () => import('../documents/history').then(m => m.AdminHistory) },
    { path: 'jira', title: adminText.jira, loadComponent: () => import('./jira-settings').then(m => m.JiraSettingsPage) },
  ] },
];

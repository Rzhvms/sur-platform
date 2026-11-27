import { Routes } from '@angular/router';
import { requireSession } from './core/auth/access';
import { ChangePasswordComponent } from './core/auth/change-password/change-password.component';
import { ForbiddenComponent } from './core/auth/forbidden/forbidden.component';
import { LoginComponent } from './core/auth/login/login.component';
import { documentText } from './features/documents/document-contract';
import { treatmentText } from './features/treatments/treatment-contract';
import { text } from './shared/text';
import { NotFoundComponent } from './shared/ui/not-found/not-found.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'risks' },
  { path: 'login', component: LoginComponent, title: text.login },
  { path: 'change-password', component: ChangePasswordComponent, canActivate: [requireSession], title: text.changePassword },
  { path: 'forbidden', component: ForbiddenComponent, canActivate: [requireSession], title: text.accessDenied },
  { path: 'help', canActivate: [requireSession], title: text.help, loadComponent: () => import('./features/help/help/help.component').then(m => m.HelpComponent) },
  { path: 'risks', canActivate: [requireSession], loadChildren: () => import('./features/risks/routes').then(m => m.riskRoutes) },
  { path: 'demo/risks', data: { demo: true }, loadChildren: () => import('./features/risks/routes').then(m => m.demoRiskRoutes) },
  { path: 'demo', pathMatch: 'full', redirectTo: 'demo/risks' },
  {
    path: 'treatments', canActivate: [requireSession], children: [
      { path: '', title: text.treatments, loadComponent: () => import('./features/treatments/treatments-page/treatments-page.component').then(m => m.TreatmentsPageComponent) },
      { path: ':id', title: treatmentText.treatment, loadComponent: () => import('./features/treatments/treatment-detail/treatment-detail.component').then(m => m.TreatmentDetailComponent) },
    ],
  },
  { path: 'approvals', canActivate: [requireSession], title: treatmentText.approvals, loadComponent: () => import('./features/treatments/approvals/approvals.component').then(m => m.ApprovalsComponent) },
  { path: 'admin', canActivate: [requireSession], loadChildren: () => import('./features/administration/routes').then(m => m.adminRoutes) },
  { path: 'administration', pathMatch: 'full', redirectTo: 'admin' },
  { path: 'administration/jira', pathMatch: 'full', redirectTo: 'admin/jira' },
  { path: 'reports', canActivate: [requireSession], title: documentText.reports, loadComponent: () => import('./features/documents/reports/reports.component').then(m => m.ReportsComponent) },
  { path: 'imports', canActivate: [requireSession], title: documentText.imports, loadComponent: () => import('./features/documents/imports/imports.component').then(m => m.ImportsComponent) },
  { path: 'documents', pathMatch: 'full', redirectTo: 'reports' },
  { path: '**', component: NotFoundComponent, title: text.notFound },
];

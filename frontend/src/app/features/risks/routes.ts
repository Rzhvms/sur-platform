import { Routes } from '@angular/router';
import { text } from '../../shared/text';
import { RiskCollectionComponent } from './risk-collection/risk-collection.component';

const collectionRoutes: Routes = [
  { path: '', pathMatch: 'full', component: RiskCollectionComponent, title: text.registryHeading },
  { path: 'board', component: RiskCollectionComponent, data: { board: true }, title: text.boardHeading },
];
const details = { path: ':id', loadComponent: () => import('./risk-detail/risk-detail.component').then(m => m.RiskDetailComponent), title: text.passport };

export const demoRiskRoutes: Routes = [...collectionRoutes, details];

export const riskRoutes: Routes = [
  ...collectionRoutes,
  { path: 'new', loadComponent: () => import('./risk-editor/risk-editor.component').then(m => m.RiskEditorComponent), title: text.newRisk },
  { path: ':id/edit', loadComponent: () => import('./risk-editor/risk-editor.component').then(m => m.RiskEditorComponent), title: text.editRisk },
  details,
];

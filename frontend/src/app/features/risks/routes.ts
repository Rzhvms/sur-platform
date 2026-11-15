import { Routes } from '@angular/router';
import { text } from '../../shared/text';
import { RiskCollection } from './collection';

const collectionRoutes: Routes = [
  { path: '', pathMatch: 'full', component: RiskCollection, title: text.registryHeading },
  { path: 'board', component: RiskCollection, data: { board: true }, title: text.boardHeading },
];
const details = { path: ':id', loadComponent: () => import('./detail').then(m => m.RiskDetail), title: text.passport };

export const demoRiskRoutes: Routes = [...collectionRoutes, details];

export const riskRoutes: Routes = [
  ...collectionRoutes,
  { path: 'new', loadComponent: () => import('./editor').then(m => m.RiskEditor), title: text.newRisk },
  { path: ':id/edit', loadComponent: () => import('./editor').then(m => m.RiskEditor), title: text.editRisk },
  details,
];

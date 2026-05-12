import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';

/**
 * Top-level route table.
 *
 * - The root layout (AppShellComponent) is guarded and wraps every authenticated
 *   feature; children are lazy-loaded.
 * - /not-linked is outside the shell so users without a linked member can
 *   see the explanatory screen without the nav chrome.
 */
export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./layout/app-shell.component').then((m) => m.AppShellComponent),
    canActivate: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'home' },
      {
        path: 'home',
        title: 'Inicio — FamilyNido',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'calendar',
        title: 'Calendario — FamilyNido',
        loadComponent: () =>
          import('./features/calendar/calendar.component').then((m) => m.CalendarComponent),
      },
      {
        path: 'calendar/accounts',
        title: 'Cuentas — FamilyNido',
        loadComponent: () =>
          import('./features/calendar/accounts/accounts.component').then((m) => m.AccountsComponent),
      },
      {
        path: 'wall',
        title: 'Muro — FamilyNido',
        loadComponent: () =>
          import('./features/wall/wall.component').then((m) => m.WallComponent),
      },
      {
        path: 'tasks',
        title: 'Tareas — FamilyNido',
        loadComponent: () =>
          import('./features/tasks/tasks.component').then((m) => m.TasksComponent),
      },
      {
        path: 'meals',
        title: 'Mesa — FamilyNido',
        loadComponent: () =>
          import('./features/meals/meals.component').then((m) => m.MealsComponent),
      },
      {
        path: 'health',
        title: 'Salud — FamilyNido',
        loadComponent: () =>
          import('./features/health/health.component').then((m) => m.HealthComponent),
      },
      {
        path: 'school',
        title: 'Cole — FamilyNido',
        loadComponent: () =>
          import('./features/school/school.component').then((m) => m.SchoolComponent),
      },
      {
        path: 'nido',
        title: 'Nido — FamilyNido',
        loadComponent: () =>
          import('./features/nido/nido.component').then((m) => m.NidoComponent),
      },
      {
        path: 'nido/:memberId',
        title: 'Miembro — FamilyNido',
        loadComponent: () =>
          import('./features/nido/member/member-detail.component').then((m) => m.MemberDetailComponent),
      },
      {
        path: 'account',
        title: 'Mi cuenta — FamilyNido',
        loadComponent: () =>
          import('./features/account/account.component').then((m) => m.AccountComponent),
      },
    ],
  },
  {
    path: 'tablet',
    title: 'Tablet — FamilyNido',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/tablet/tablet.component').then((m) => m.TabletComponent),
  },
  {
    path: 'login',
    title: 'Iniciar sesión — FamilyNido',
    loadComponent: () =>
      import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'invite/:token',
    title: 'Invitación — FamilyNido',
    loadComponent: () =>
      import('./features/invitations/accept-invitation.component').then((m) => m.AcceptInvitationComponent),
  },
  {
    path: 'not-linked',
    loadComponent: () =>
      import('./features/not-linked/not-linked.component').then((m) => m.NotLinkedComponent),
  },
  { path: '**', redirectTo: '' },
];

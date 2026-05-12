import { IconName } from '../shared/ui/icon/icons';

/** One entry in the primary navigation. */
export interface NavTab {
  id: string;
  path: string;
  label: string;
  icon: IconName;
}

/**
 * Primary navigation of the app. Routes use the warm localized labels
 * from the prototype ("El Nido", "La Mesa", "El Muro"). Phase 0 ships the
 * five core modules; Salud and Cole are added by their respective phases.
 */
export const NAV_TABS: readonly NavTab[] = [
  { id: 'home', path: '/home', label: $localize`:@@nav.home:Inicio`, icon: 'home' },
  { id: 'calendar', path: '/calendar', label: $localize`:@@nav.calendar:Calendario`, icon: 'calendar' },
  { id: 'wall', path: '/wall', label: $localize`:@@nav.wall:Muro`, icon: 'wall' },
  { id: 'tasks', path: '/tasks', label: $localize`:@@nav.tasks:Tareas`, icon: 'tasks' },
  { id: 'meals', path: '/meals', label: $localize`:@@nav.meals:Mesa`, icon: 'meal' },
  { id: 'health', path: '/health', label: $localize`:@@nav.health:Salud`, icon: 'health' },
  { id: 'school', path: '/school', label: $localize`:@@nav.school:Cole`, icon: 'school' },
  { id: 'nido', path: '/nido', label: $localize`:@@nav.family:Nido`, icon: 'contacts' },
] as const;

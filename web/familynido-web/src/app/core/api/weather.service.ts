import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { WeatherToday } from '../models/weather';

/** Thin client for `/api/weather`. */
@Injectable({ providedIn: 'root' })
export class WeatherService {
  private readonly http = inject(HttpClient);

  /**
   * Today's snapshot for the family location. Returns 404 when the family
   * has no location configured — callers treat that as "hide the widget".
   */
  today(): Observable<WeatherToday> {
    return this.http.get<WeatherToday>('/api/weather/today');
  }
}

import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  HealthProfile,
  Medication,
  MedicationRequest,
  MemberHealth,
  UpsertHealthProfileRequest,
  Vaccination,
  VaccinationRequest,
} from '../models/health';

/** Thin client for the `/api/health` slice family. */
@Injectable({ providedIn: 'root' })
export class HealthService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/health';

  getMember(memberId: string): Observable<MemberHealth> {
    return this.http.get<MemberHealth>(`${this.baseUrl}/members/${memberId}`);
  }

  upsertProfile(memberId: string, body: UpsertHealthProfileRequest): Observable<HealthProfile> {
    return this.http.put<HealthProfile>(`${this.baseUrl}/members/${memberId}/profile`, body);
  }

  addVaccination(memberId: string, body: VaccinationRequest): Observable<Vaccination> {
    return this.http.post<Vaccination>(`${this.baseUrl}/members/${memberId}/vaccinations`, body);
  }

  updateVaccination(id: string, body: VaccinationRequest): Observable<Vaccination> {
    return this.http.put<Vaccination>(`${this.baseUrl}/vaccinations/${id}`, body);
  }

  deleteVaccination(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/vaccinations/${id}`);
  }

  addMedication(memberId: string, body: MedicationRequest): Observable<Medication> {
    return this.http.post<Medication>(`${this.baseUrl}/members/${memberId}/medications`, body);
  }

  updateMedication(id: string, body: MedicationRequest): Observable<Medication> {
    return this.http.put<Medication>(`${this.baseUrl}/medications/${id}`, body);
  }

  deleteMedication(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/medications/${id}`);
  }
}

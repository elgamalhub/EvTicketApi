import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { AdminDashboard, AdminUser, OrganizerDashboard, PagedResult } from './models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly api = environment.apiUrl;

  organizer(): Observable<OrganizerDashboard> {
    return this.http.get<OrganizerDashboard>(`${this.api}/api/dashboard/organizer`);
  }

  admin(): Observable<AdminDashboard> {
    return this.http.get<AdminDashboard>(`${this.api}/api/dashboard/admin`);
  }

  users(page = 1, pageSize = 20, q?: string): Observable<PagedResult<AdminUser>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (q) params = params.set('q', q);
    return this.http.get<PagedResult<AdminUser>>(`${this.api}/api/dashboard/users`, { params });
  }

  setOrganizer(userId: string, promote: boolean): Observable<void> {
    return this.http.post<void>(`${this.api}/api/dashboard/users/${userId}/organizer?promote=${promote}`, {});
  }

  deleteUser(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.api}/api/dashboard/users/${userId}`);
  }
}

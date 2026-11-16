import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { CreateUrlRequest, PagedResult, UrlResponse } from '../models/url.model';

@Injectable({
  providedIn: 'root'
})
export class UrlShortenerService {
  // Configured to match ASP.NET Core API server default HTTP endpoint
  private readonly baseUrl = 'http://localhost:5000';
  private readonly apiUrl = `${this.baseUrl}/api/urls`;

  constructor(private readonly http: HttpClient) {}

  /**
   * Creates a new shortened URL
   */
  createShortUrl(request: CreateUrlRequest): Observable<UrlResponse> {
    return this.http.post<UrlResponse>(this.apiUrl, request);
  }

  /**
   * Retrieves paginated list of shortened URLs
   */
  getPagedUrls(pageNumber: number = 1, pageSize: number = 10): Observable<PagedResult<UrlResponse>> {
    const params = new HttpParams()
      .set('pageNumber', pageNumber.toString())
      .set('pageSize', pageSize.toString());

    return this.http.get<PagedResult<UrlResponse>>(this.apiUrl, { params });
  }

  /**
   * Retrieves details for a specific short code
   */
  getUrlByShortCode(shortCode: string): Observable<UrlResponse> {
    return this.http.get<UrlResponse>(`${this.apiUrl}/${shortCode}`);
  }

  /**
   * Deactivates a shortened URL
   */
  deactivateUrl(shortCode: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${shortCode}`);
  }
}

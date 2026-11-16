import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { UrlShortenerService } from './services/url-shortener.service';
import { PagedResult, UrlResponse } from './models/url.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  title = 'ShortLink - Scalable URL Shortener';

  urlForm: FormGroup;
  generatedUrl: UrlResponse | null = null;
  pagedResult: PagedResult<UrlResponse> | null = null;

  currentPage: number = 1;
  pageSize: number = 10;

  loading: boolean = false;
  tableLoading: boolean = false;
  errorMessage: string | null = null;
  copySuccess: boolean = false;
  tableCopyIndex: number | null = null;

  constructor(
    private readonly fb: FormBuilder,
    private readonly urlService: UrlShortenerService
  ) {
    this.urlForm = this.fb.group({
      originalUrl: ['', [Validators.required, Validators.pattern('^https?://.+')]],
      expiresAt: ['']
    });
  }

  ngOnInit(): void {
    this.loadUrls();
  }

  loadUrls(page: number = 1): void {
    this.currentPage = page;
    this.tableLoading = true;

    this.urlService.getPagedUrls(this.currentPage, this.pageSize).subscribe({
      next: (result) => {
        this.pagedResult = result;
        this.tableLoading = false;
      },
      error: (err) => {
        console.error('Failed to load URLs', err);
        this.tableLoading = false;
      }
    });
  }

  onShortenUrl(): void {
    if (this.urlForm.invalid) {
      this.urlForm.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.errorMessage = null;
    this.copySuccess = false;

    const formVal = this.urlForm.value;
    const expiresAt = formVal.expiresAt ? new Date(formVal.expiresAt).toISOString() : null;

    this.urlService.createShortUrl({
      originalUrl: formVal.originalUrl.trim(),
      expiresAt: expiresAt
    }).subscribe({
      next: (response) => {
        this.generatedUrl = response;
        this.loading = false;
        this.urlForm.reset();
        this.loadUrls(1);
      },
      error: (err) => {
        this.loading = false;
        if (err.status === 429) {
          this.errorMessage = 'Rate limit exceeded! Maximum 100 requests per minute. Please try again later.';
        } else if (err.error?.detail) {
          this.errorMessage = err.error.detail;
        } else {
          this.errorMessage = 'An error occurred while shortening the URL. Please verify your input.';
        }
      }
    });
  }

  copyToClipboard(text: string, isTableIndex?: number): void {
    if (!navigator.clipboard) {
      // Fallback for browsers without navigator.clipboard
      const el = document.createElement('textarea');
      el.value = text;
      document.body.appendChild(el);
      el.select();
      document.execCommand('copy');
      document.body.removeChild(el);
    } else {
      navigator.clipboard.writeText(text);
    }

    if (isTableIndex !== undefined) {
      this.tableCopyIndex = isTableIndex;
      setTimeout(() => (this.tableCopyIndex = null), 2000);
    } else {
      this.copySuccess = true;
      setTimeout(() => (this.copySuccess = false), 2000);
    }
  }

  openUrl(url: string): void {
    window.open(url, '_blank', 'noopener,noreferrer');
  }

  deactivateUrl(shortCode: string): void {
    if (!confirm(`Are you sure you want to deactivate short link '${shortCode}'?`)) {
      return;
    }

    this.urlService.deactivateUrl(shortCode).subscribe({
      next: () => {
        this.loadUrls(this.currentPage);
      },
      error: (err) => {
        alert('Failed to deactivate URL: ' + (err.error?.detail || err.message));
      }
    });
  }

  isExpired(dateString: string | null): boolean {
    if (!dateString) return false;
    return new Date(dateString) <= new Date();
  }
}

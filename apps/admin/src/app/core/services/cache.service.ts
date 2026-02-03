import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { ApiService } from "./api.service";

export interface CacheStatus {
  totalSizeBytes: number;
  fileCount: number;
  maxSizeBytes: number;
  usagePercent: number;
}

export interface CachedFileDto {
  fileHash: string;
  originalUrl: string;
  providerId: number;
  providerName?: string;
  fileSizeBytes: number;
  contentType: string;
  cachedDate: string;
  lastAccessedDate: string;
  accessCount: number;
}

export interface PagedResult<T> {
  data: T[];
  pagination: {
    page: number;
    pageSize: number;
    totalItems: number;
    totalPages: number;
  };
}

@Injectable({
  providedIn: "root",
})
export class CacheService {
  private readonly api = inject(ApiService);

  getCacheStatus(): Observable<CacheStatus> {
    return this.api.get<CacheStatus>("/api/admin/cache/status");
  }

  getCachedFiles(
    page: number = 1,
    pageSize: number = 50
  ): Observable<PagedResult<CachedFileDto>> {
    return this.api.get<PagedResult<CachedFileDto>>("/api/admin/cache/files", {
      page,
      pageSize,
    });
  }

  clearCache(): Observable<{ message: string }> {
    return this.api.post<{ message: string }>("/api/admin/cache/clear");
  }

  triggerEviction(): Observable<{ filesEvicted: number }> {
    return this.api.post<{ filesEvicted: number }>("/api/admin/cache/evict");
  }

  deleteCachedFile(fileHash: string): Observable<void> {
    return this.api.delete<void>(`/api/admin/cache/files/${fileHash}`);
  }
}

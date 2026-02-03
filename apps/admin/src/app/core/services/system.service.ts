import { inject, Injectable } from "@angular/core";
import { Observable } from "rxjs";
import { ApiService } from "./api.service";
import {
  SystemInfoResponse,
  UpdateCheckResponse,
  UpdateTriggerResponse,
} from "../models";

/**
 * Service for system information and update management.
 */
@Injectable({
  providedIn: "root",
})
export class SystemService {
  private readonly api = inject(ApiService);

  /**
   * Get current system information including version and update status.
   */
  getSystemInfo(): Observable<SystemInfoResponse> {
    return this.api.get<SystemInfoResponse>("/api/admin/system/info");
  }

  /**
   * Check for available updates (uses cached result if available).
   */
  checkForUpdates(): Observable<UpdateCheckResponse> {
    return this.api.get<UpdateCheckResponse>("/api/admin/system/updates");
  }

  /**
   * Force a fresh check for updates, bypassing the cache.
   */
  forceUpdateCheck(): Observable<UpdateCheckResponse> {
    return this.api.post<UpdateCheckResponse>("/api/admin/system/updates/check");
  }

  /**
   * Trigger the update process. This will update the application and restart.
   */
  applyUpdate(): Observable<UpdateTriggerResponse> {
    return this.api.post<UpdateTriggerResponse>("/api/admin/system/update");
  }
}

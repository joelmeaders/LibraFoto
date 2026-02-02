import { Component, inject, signal, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatBadgeModule } from "@angular/material/badge";
import { MatDialog, MatDialogModule } from "@angular/material/dialog";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { SystemService } from "../../../core/services/system.service";
import { SystemInfoResponse } from "../../../core/models";
import { UpdateConfirmDialogComponent } from "./update-confirm-dialog.component";

@Component({
  selector: "app-system-card",
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatBadgeModule,
    MatDialogModule,
    MatSnackBarModule,
  ],
  templateUrl: "./system-card.component.html",
  styleUrls: ["./system-card.component.scss"],
})
export class SystemCardComponent implements OnInit {
  private readonly systemService = inject(SystemService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  // Reactive state
  readonly systemInfo = signal<SystemInfoResponse | null>(null);
  readonly isLoading = signal(true);
  readonly isCheckingUpdates = signal(false);
  readonly isUpdating = signal(false);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.loadSystemInfo();
  }

  /**
   * Load system information from the server.
   */
  refreshInfo(): void {
    this.loadSystemInfo();
  }

  /**
   * Force check for updates.
   */
  checkUpdates(): void {
    this.isCheckingUpdates.set(true);
    this.error.set(null);

    this.systemService.forceUpdateCheck().subscribe({
      next: (result) => {
        this.isCheckingUpdates.set(false);
        // Reload system info to get updated info
        this.loadSystemInfo();

        if (result.error) {
          this.snackBar.open(`Update check error: ${result.error}`, "Dismiss", {
            duration: 5000,
          });
        } else if (result.updateAvailable) {
          this.snackBar.open(
            `Update available: ${result.latestVersion} (${result.commitsBehind} commits behind)`,
            "Dismiss",
            { duration: 5000 }
          );
        } else {
          this.snackBar.open("You are running the latest version", "Dismiss", {
            duration: 3000,
          });
        }
      },
      error: (err) => {
        this.isCheckingUpdates.set(false);
        this.error.set("Failed to check for updates");
        console.error("Failed to check for updates:", err);
      },
    });
  }

  /**
   * Show confirmation dialog and trigger update if confirmed.
   */
  triggerUpdate(): void {
    const dialogRef = this.dialog.open(UpdateConfirmDialogComponent, {
      width: "400px",
      data: {
        latestVersion: this.systemInfo()?.latestVersion,
        commitsBehind: this.systemInfo()?.commitsBehind,
        changelog: this.systemInfo()?.changelog,
      },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (confirmed) {
        this.applyUpdate();
      }
    });
  }

  /**
   * Format uptime duration for display.
   */
  formatUptime(uptime: string): string {
    // Parse .NET TimeSpan format (e.g., "1.02:30:45" or "02:30:45")
    const parts = uptime.split(":");
    if (parts.length < 2) return uptime;

    let days = 0;
    let hours = 0;
    let minutes = 0;

    if (parts[0].includes(".")) {
      const dayHour = parts[0].split(".");
      days = parseInt(dayHour[0], 10);
      hours = parseInt(dayHour[1], 10);
    } else {
      hours = parseInt(parts[0], 10);
    }
    minutes = parseInt(parts[1], 10);

    const result: string[] = [];
    if (days > 0) result.push(`${days}d`);
    if (hours > 0) result.push(`${hours}h`);
    if (minutes > 0 || result.length === 0) result.push(`${minutes}m`);

    return result.join(" ");
  }

  private loadSystemInfo(): void {
    this.isLoading.set(true);
    this.error.set(null);

    this.systemService.getSystemInfo().subscribe({
      next: (info) => {
        this.systemInfo.set(info);
        this.isLoading.set(false);
      },
      error: (err) => {
        this.error.set("Failed to load system information");
        this.isLoading.set(false);
        console.error("Failed to load system info:", err);
      },
    });
  }

  private applyUpdate(): void {
    this.isUpdating.set(true);

    this.systemService.applyUpdate().subscribe({
      next: (result) => {
        this.snackBar.open(
          `${result.message}. The application will restart shortly.`,
          "OK",
          { duration: 10000 }
        );
        // Keep isUpdating true since the server will restart
      },
      error: (err) => {
        this.isUpdating.set(false);
        this.snackBar.open("Failed to trigger update. Please try again.", "Dismiss", {
          duration: 5000,
        });
        console.error("Failed to trigger update:", err);
      },
    });
  }
}

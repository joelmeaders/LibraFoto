import { Component, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { ActivatedRoute } from "@angular/router";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatIconModule } from "@angular/material/icon";
import { HttpClient } from "@angular/common/http";
import { take } from "rxjs/operators";

/**
 * OAuth callback page that handles the redirect from Google OAuth.
 * Captures the authorization code, sends it to the backend, and closes the popup.
 */
@Component({
  selector: "app-oauth-callback",
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule, MatIconModule],
  template: `
    <div class="callback-container">
      @if (status === "processing") {
        <div class="status processing">
          <mat-spinner diameter="60"></mat-spinner>
          <h2>Connecting to Google Photos...</h2>
          <p>Please wait while we complete the authorization.</p>
        </div>
      } @else if (status === "success") {
        <div class="status success">
          <mat-icon>check_circle</mat-icon>
          <h2>Successfully Connected!</h2>
          <p>You can close this window now.</p>
        </div>
      } @else if (status === "error") {
        <div class="status error">
          <mat-icon>error</mat-icon>
          <h2>Connection Failed</h2>
          <p>{{ errorMessage }}</p>
          <p class="help-text">Please close this window and try again.</p>
        </div>
      }
    </div>
  `,
  styles: [
    `
      .callback-container {
        display: flex;
        align-items: center;
        justify-content: center;
        min-height: 100vh;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        padding: 24px;
      }

      .status {
        background: white;
        border-radius: 16px;
        padding: 48px;
        max-width: 400px;
        text-align: center;
        box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
      }

      .status mat-icon {
        font-size: 80px;
        width: 80px;
        height: 80px;
        margin-bottom: 16px;
      }

      .processing mat-icon,
      .processing mat-spinner {
        color: #2196f3;
        margin: 0 auto 24px;
      }

      .success mat-icon {
        color: #4caf50;
      }

      .error mat-icon {
        color: #f44336;
      }

      h2 {
        margin: 0 0 12px 0;
        font-size: 24px;
        font-weight: 500;
        color: #333;
      }

      p {
        margin: 0;
        color: #666;
        font-size: 14px;
        line-height: 1.5;
      }

      .help-text {
        margin-top: 12px;
        font-size: 12px;
        color: #999;
      }
    `,
  ],
})
export class OAuthCallbackComponent implements OnInit {
  status: "processing" | "success" | "error" = "processing";
  errorMessage = "";

  constructor(
    private readonly route: ActivatedRoute,
    private readonly http: HttpClient,
  ) {}

  ngOnInit(): void {
    this.handleCallback();
  }

  private handleCallback(): void {
    // Get query parameters from URL
    this.route.queryParams.pipe(take(1)).subscribe((params) => {
      const code = params["code"];
      const state = params["state"]; // Provider ID
      const error = params["error"];

      if (error) {
        this.status = "error";
        this.errorMessage = `Authorization failed: ${error}`;
        this.autoCloseWindow();
        return;
      }

      if (!code) {
        this.status = "error";
        this.errorMessage = "No authorization code received";
        this.autoCloseWindow();
        return;
      }

      if (!state) {
        this.status = "error";
        this.errorMessage = "No provider ID received";
        this.autoCloseWindow();
        return;
      }

      // Send authorization code to backend
      this.http
        .post(`/api/storage/google-photos/${state}/callback`, {
          authorizationCode: code,
        })
        .subscribe({
          next: () => {
            this.status = "success";
            this.autoCloseWindow(1000);
          },
          error: (err) => {
            this.status = "error";
            this.errorMessage =
              err.error?.message ||
              "Failed to connect to Google Photos. Please try again.";
            this.autoCloseWindow(3000);
          },
        });
    });
  }

  private autoCloseWindow(delay: number = 2000): void {
    // Auto-close popup after delay
    setTimeout(() => {
      if (window.opener) {
        window.close();
      }
    }, delay);
  }
}

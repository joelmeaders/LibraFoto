import { Component, inject, signal, ViewChild } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
} from "@angular/forms";
import { Router } from "@angular/router";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatInputModule } from "@angular/material/input";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatStepperModule, MatStepper } from "@angular/material/stepper";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatRadioModule } from "@angular/material/radio";
import { MatCheckboxModule } from "@angular/material/checkbox";
import { AuthService } from "../../core/services/auth.service";

@Component({
  selector: "app-setup",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatStepperModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatRadioModule,
    MatCheckboxModule,
  ],
  template: `
    <div class="setup-container">
      <mat-card class="setup-card">
        <div class="logo">
          <mat-icon class="logo-icon">photo_camera</mat-icon>
          <h1>Welcome to LibraFoto</h1>
          <p class="subtitle">Let's set up your digital picture frame</p>
        </div>

        <mat-card-content>
          <mat-stepper #stepper orientation="vertical" linear>
            <!-- Step 1: Admin Account -->
            <mat-step [stepControl]="adminForm" label="Create Admin Account">
              <form [formGroup]="adminForm" class="step-content">
                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Email</mat-label>
                  <input
                    matInput
                    type="email"
                    formControlName="email"
                    placeholder="admin@example.com"
                  />
                  <mat-icon matSuffix>email</mat-icon>
                  @if (adminForm.get('email')?.hasError('required') &&
                  adminForm.get('email')?.touched) {
                  <mat-error>Email is required</mat-error>
                  } @if (adminForm.get('email')?.hasError('email')) {
                  <mat-error>Please enter a valid email address</mat-error>
                  }
                </mat-form-field>

                <mat-form-field appearance="outline" class="full-width">
                  <mat-label>Password</mat-label>
                  <input
                    matInput
                    [type]="hidePassword() ? 'password' : 'text'"
                    formControlName="password"
                  />
                  <button
                    mat-icon-button
                    matSuffix
                    type="button"
                    (click)="hidePassword.set(!hidePassword())"
                  >
                    <mat-icon>{{
                      hidePassword() ? "visibility_off" : "visibility"
                    }}</mat-icon>
                  </button>
                  @if (adminForm.get('password')?.hasError('required') &&
                  adminForm.get('password')?.touched) {
                  <mat-error>Password is required</mat-error>
                  } @if (adminForm.get('password')?.hasError('minlength')) {
                  <mat-error>Password must be at least 8 characters</mat-error>
                  }
                </mat-form-field>

                <div class="step-actions">
                  <button
                    mat-raised-button
                    color="primary"
                    matStepperNext
                    [disabled]="adminForm.invalid"
                  >
                    Next
                    <mat-icon>arrow_forward</mat-icon>
                  </button>
                </div>
              </form>
            </mat-step>

            <!-- Step 2: Storage Configuration -->
            <mat-step label="Configure Storage" optional>
              <div class="step-content">
                <p class="step-description">
                  Choose where to store your photos. You can add more sources
                  later.
                </p>

                <div class="storage-options">
                  <button
                    mat-stroked-button
                    class="storage-option"
                    [class.selected]="selectedStorage() === 'local'"
                    (click)="selectedStorage.set('local')"
                  >
                    <mat-icon>folder</mat-icon>
                    <div class="storage-option-text">
                      <strong>Local Storage</strong>
                      <span>Store photos on this device</span>
                    </div>
                    @if (selectedStorage() === 'local') {
                    <mat-icon class="check-icon">check_circle</mat-icon>
                    }
                  </button>

                  <button
                    mat-stroked-button
                    class="storage-option"
                    [class.selected]="selectedStorage() === 'google-photos'"
                    (click)="selectedStorage.set('google-photos')"
                    disabled
                  >
                    <mat-icon>add_to_drive</mat-icon>
                    <div class="storage-option-text">
                      <strong>Google Photos</strong>
                      <span>Coming soon</span>
                    </div>
                  </button>

                  <button
                    mat-stroked-button
                    class="storage-option"
                    [class.selected]="selectedStorage() === 'google-drive'"
                    (click)="selectedStorage.set('google-drive')"
                    disabled
                  >
                    <mat-icon>cloud</mat-icon>
                    <div class="storage-option-text">
                      <strong>Google Drive</strong>
                      <span>Coming soon</span>
                    </div>
                  </button>
                </div>

                <div class="step-actions">
                  <button mat-button matStepperPrevious>
                    <mat-icon>arrow_back</mat-icon>
                    Back
                  </button>
                  <button mat-raised-button color="primary" matStepperNext>
                    Next
                    <mat-icon>arrow_forward</mat-icon>
                  </button>
                </div>
              </div>
            </mat-step>

            <!-- Step 3: Complete -->
            <mat-step label="Complete Setup">
              <div class="step-content">
                <div class="complete-icon">
                  <mat-icon>check_circle</mat-icon>
                </div>
                <h2>Ready to go!</h2>
                <p class="step-description">
                  Your LibraFoto is almost ready. Click "Complete Setup" to
                  create your account and start using your digital picture
                  frame.
                </p>

                <div class="summary">
                  <h3>Summary</h3>
                  <div class="summary-item">
                    <mat-icon>email</mat-icon>
                    <span
                      >Admin account:
                      <strong>{{ adminForm.get("email")?.value }}</strong></span
                    >
                  </div>
                  <div class="summary-item">
                    <mat-icon>folder</mat-icon>
                    <span
                      >Storage: <strong>{{ getStorageName() }}</strong></span
                    >
                  </div>
                </div>

                @if (errorMessage()) {
                <div class="error-message">
                  <mat-icon>error</mat-icon>
                  <span>{{ errorMessage() }}</span>
                </div>
                }

                <div class="step-actions">
                  <button
                    mat-button
                    matStepperPrevious
                    [disabled]="isLoading()"
                  >
                    <mat-icon>arrow_back</mat-icon>
                    Back
                  </button>
                  <button
                    mat-raised-button
                    color="primary"
                    (click)="completeSetup()"
                    [disabled]="isLoading()"
                  >
                    @if (isLoading()) {
                    <mat-spinner diameter="20"></mat-spinner>
                    } @else {
                    <ng-container>
                      Complete Setup
                      <mat-icon>check</mat-icon>
                    </ng-container>
                    }
                  </button>
                </div>
              </div>
            </mat-step>
          </mat-stepper>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [
    `
      .setup-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        padding: 24px;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      }

      .setup-card {
        max-width: 550px;
        width: 100%;
        padding: 24px;
      }

      .logo {
        text-align: center;
        margin-bottom: 24px;
      }

      .logo-icon {
        font-size: 48px;
        width: 48px;
        height: 48px;
        color: #667eea;
      }

      .logo h1 {
        margin: 8px 0 0;
        font-weight: 300;
        font-size: 24px;
      }

      .subtitle {
        color: rgba(0, 0, 0, 0.6);
        margin: 4px 0 0;
      }

      .step-content {
        padding: 16px 0;
      }

      .step-description {
        color: rgba(0, 0, 0, 0.7);
        margin-bottom: 24px;
      }

      .step-actions {
        display: flex;
        gap: 8px;
        margin-top: 24px;
        justify-content: flex-end;
      }

      .full-width {
        width: 100%;
      }

      .storage-options {
        display: flex;
        flex-direction: column;
        gap: 12px;
      }

      .storage-option {
        display: flex;
        align-items: center;
        gap: 16px;
        padding: 16px;
        text-align: left;
        border-radius: 8px;
        height: auto;
        justify-content: flex-start;
      }

      .storage-option.selected {
        border-color: #667eea;
        background-color: rgba(102, 126, 234, 0.05);
      }

      .storage-option-text {
        display: flex;
        flex-direction: column;
        flex: 1;
      }

      .storage-option-text span {
        font-size: 12px;
        color: rgba(0, 0, 0, 0.5);
      }

      .check-icon {
        color: #667eea;
      }

      .complete-icon {
        text-align: center;
        margin-bottom: 16px;
      }

      .complete-icon mat-icon {
        font-size: 64px;
        width: 64px;
        height: 64px;
        color: #4caf50;
      }

      .step-content h2 {
        text-align: center;
        margin: 0 0 16px;
      }

      .summary {
        background: rgba(0, 0, 0, 0.03);
        border-radius: 8px;
        padding: 16px;
        margin: 24px 0;
      }

      .summary h3 {
        margin: 0 0 12px;
        font-size: 14px;
        font-weight: 500;
        color: rgba(0, 0, 0, 0.6);
      }

      .summary-item {
        display: flex;
        align-items: center;
        gap: 12px;
        padding: 8px 0;
      }

      .summary-item mat-icon {
        color: rgba(0, 0, 0, 0.5);
      }

      .error-message {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 12px;
        background: #ffebee;
        border-radius: 4px;
        color: #c62828;
        margin: 16px 0;
      }

      .error-message mat-icon {
        font-size: 20px;
        width: 20px;
        height: 20px;
      }
    `,
  ],
})
export class SetupComponent {
  @ViewChild("stepper") stepper!: MatStepper;

  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  adminForm: FormGroup = this.fb.group({
    email: ["", [Validators.required, Validators.email]],
    password: ["", [Validators.required, Validators.minLength(8)]],
  });

  hidePassword = signal(true);
  selectedStorage = signal<"local" | "google-photos" | "google-drive">("local");
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  getStorageName(): string {
    switch (this.selectedStorage()) {
      case "local":
        return "Local Storage";
      case "google-photos":
        return "Google Photos";
      case "google-drive":
        return "Google Drive";
      default:
        return "Not configured";
    }
  }

  completeSetup(): void {
    if (this.adminForm.invalid) {
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.adminForm.value;

    this.authService
      .setup({
        email,
        password,
      })
      .subscribe({
        next: () => {
          this.isLoading.set(false);
          this.snackBar.open("Setup complete! Welcome to LibraFoto.", "Close", {
            duration: 5000,
          });
          this.router.navigate(["/dashboard"]);
        },
        error: (error) => {
          this.isLoading.set(false);
          this.errorMessage.set(
            error.message || "Setup failed. Please try again."
          );
        },
      });
  }
}

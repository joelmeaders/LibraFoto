import { Component, inject, signal } from "@angular/core";
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
import { MatIconModule } from "@angular/material/icon";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { AuthService } from "../../core/services/auth.service";

@Component({
  selector: "app-login",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatInputModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  template: `
    <div class="login-container">
      <mat-card class="login-card">
        <div class="logo">
          <mat-icon class="logo-icon">photo_camera</mat-icon>
          <h1>LibraFoto</h1>
        </div>

        <mat-card-content>
          <form [formGroup]="loginForm" (ngSubmit)="onSubmit()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Email</mat-label>
              <input
                matInput
                formControlName="email"
                type="email"
                autocomplete="email"
                [attr.aria-label]="'Email'"
              />
              <mat-icon matSuffix>person</mat-icon>
              @if (loginForm.get('email')?.hasError('required') &&
              loginForm.get('email')?.touched) {
              <mat-error>Email is required</mat-error>
              }
            </mat-form-field>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Password</mat-label>
              <input
                matInput
                [type]="hidePassword() ? 'password' : 'text'"
                formControlName="password"
                autocomplete="current-password"
                [attr.aria-label]="'Password'"
              />
              <button
                mat-icon-button
                matSuffix
                type="button"
                (click)="hidePassword.set(!hidePassword())"
                [attr.aria-label]="
                  hidePassword() ? 'Show password' : 'Hide password'
                "
              >
                <mat-icon>{{
                  hidePassword() ? "visibility_off" : "visibility"
                }}</mat-icon>
              </button>
              @if (loginForm.get('password')?.hasError('required') &&
              loginForm.get('password')?.touched) {
              <mat-error>Password is required</mat-error>
              }
            </mat-form-field>

            @if (errorMessage()) {
            <div class="error-message">
              <mat-icon>error</mat-icon>
              <span>{{ errorMessage() }}</span>
            </div>
            }

            <button
              mat-raised-button
              color="primary"
              class="full-width submit-button"
              type="submit"
              [disabled]="isLoading()"
            >
              @if (isLoading()) {
              <mat-spinner diameter="20"></mat-spinner>
              } @else { Sign In }
            </button>
          </form>
        </mat-card-content>

        <mat-card-footer class="footer">
          <p>Your personal photo frame</p>
        </mat-card-footer>
      </mat-card>
    </div>
  `,
  styles: [
    `
      .login-container {
        display: flex;
        justify-content: center;
        align-items: center;
        min-height: 100vh;
        background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        padding: 16px;
      }

      .login-card {
        width: 100%;
        max-width: 400px;
        padding: 32px;
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
        font-size: 28px;
      }

      .full-width {
        width: 100%;
      }

      mat-form-field {
        margin-bottom: 8px;
      }

      .submit-button {
        margin-top: 16px;
        height: 48px;
        font-size: 16px;
      }

      .submit-button mat-spinner {
        display: inline-block;
      }

      .error-message {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 12px;
        background: #ffebee;
        border-radius: 4px;
        color: #c62828;
        margin-bottom: 16px;
      }

      .error-message mat-icon {
        font-size: 20px;
        width: 20px;
        height: 20px;
      }

      .footer {
        text-align: center;
        padding: 16px 0 0;
        margin-top: 16px;
        border-top: 1px solid rgba(0, 0, 0, 0.1);
      }

      .footer p {
        margin: 0;
        color: rgba(0, 0, 0, 0.5);
        font-size: 14px;
      }
    `,
  ],
})
export class LoginComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  loginForm: FormGroup = this.fb.group({
    email: ["", [Validators.required, Validators.email]],
    password: ["", [Validators.required]],
  });

  hidePassword = signal(true);
  isLoading = signal(false);
  errorMessage = signal<string | null>(null);

  constructor() {
    // Check if setup is required
    this.checkSetupStatus();
  }

  private async checkSetupStatus(): Promise<void> {
    try {
      const status = await this.authService.checkSetupStatus().toPromise();
      if (status?.isSetupRequired) {
        this.router.navigate(["/setup"]);
      }
    } catch (error) {
      console.error("Failed to check setup status:", error);
    }
  }

  onSubmit(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    const { email, password } = this.loginForm.value;

    this.authService.login({ email, password }).subscribe({
      next: () => {
        this.isLoading.set(false);
        this.snackBar.open("Welcome back!", "Close", { duration: 3000 });
        this.router.navigate(["/dashboard"]);
      },
      error: (error) => {
        this.isLoading.set(false);
        this.errorMessage.set(error.message || "Invalid email or password");
      },
    });
  }
}

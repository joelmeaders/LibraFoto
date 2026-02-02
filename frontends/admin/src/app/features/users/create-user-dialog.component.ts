import { Component, inject, OnInit, signal } from "@angular/core";
import { CommonModule } from "@angular/common";
import {
  FormsModule,
  ReactiveFormsModule,
  FormBuilder,
  FormGroup,
  Validators,
  AbstractControl,
  ValidationErrors,
} from "@angular/forms";
import {
  MatDialogModule,
  MatDialogRef,
  MAT_DIALOG_DATA,
} from "@angular/material/dialog";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatSelectModule } from "@angular/material/select";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { AuthService } from "../../core/services/auth.service";
import { UserDto } from "../../core/models";
import { UserRole } from "../../core/models/enums.model";
import { debounceTime, distinctUntilChanged, switchMap, of } from "rxjs";

interface DialogData {
  user?: UserDto;
}

@Component({
  selector: "app-create-user-dialog",
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSelectModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEditMode ? "Edit User" : "Create User" }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email</mat-label>
          <input
            matInput
            formControlName="email"
            placeholder="Enter email"
            type="email"
          />
          @if (form.get('email')?.hasError('required') &&
          form.get('email')?.touched) {
          <mat-error>Email is required</mat-error>
          } @if (form.get('email')?.hasError('email')) {
          <mat-error>Please enter a valid email</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>{{
            isEditMode
              ? "New Password (leave blank to keep current)"
              : "Password"
          }}</mat-label>
          <input
            matInput
            formControlName="password"
            [type]="hidePassword() ? 'password' : 'text'"
            placeholder="Enter password"
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
          @if (form.get('password')?.hasError('required') &&
          form.get('password')?.touched) {
          <mat-error>Password is required</mat-error>
          } @if (form.get('password')?.hasError('minlength')) {
          <mat-error>Password must be at least 8 characters</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">
            <mat-option [value]="UserRole.Admin"
              >Admin - Full access</mat-option
            >
            <mat-option [value]="UserRole.Editor"
              >Editor - Manage photos, albums, tags</mat-option
            >
            <mat-option [value]="UserRole.Guest">Guest - View only</mat-option>
          </mat-select>
          @if (form.get('role')?.hasError('required') &&
          form.get('role')?.touched) {
          <mat-error>Role is required</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close [disabled]="isSaving()">
        Cancel
      </button>
      <button
        mat-raised-button
        color="primary"
        (click)="save()"
        [disabled]="form.invalid || isSaving()"
      >
        @if (isSaving()) {
        <mat-spinner diameter="20"></mat-spinner>
        } @else {
        {{ isEditMode ? "Save" : "Create" }}
        }
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      mat-dialog-content {
        min-width: 350px;
      }

      .full-width {
        width: 100%;
        margin-bottom: 8px;
      }

      mat-dialog-actions button mat-spinner {
        display: inline-block;
      }
    `,
  ],
})
export class CreateUserDialogComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(MatDialogRef<CreateUserDialogComponent>);
  private readonly authService = inject(AuthService);
  private readonly data = inject<DialogData | null>(MAT_DIALOG_DATA, {
    optional: true,
  });

  form!: FormGroup;
  isSaving = signal(false);
  hidePassword = signal(true);
  isEditMode = false;

  UserRole = UserRole;

  ngOnInit(): void {
    this.isEditMode = !!this.data?.user;

    this.form = this.fb.group({
      email: [
        this.data?.user?.email ?? "",
        [Validators.required, Validators.email],
      ],
      password: [
        "",
        this.isEditMode
          ? [Validators.minLength(8)]
          : [Validators.required, Validators.minLength(8)],
      ],
      role: [this.data?.user?.role ?? UserRole.Editor, [Validators.required]],
    });
  }

  save(): void {
    if (this.form.invalid) return;

    this.isSaving.set(true);
    const { email, password, role } = this.form.value;

    if (this.isEditMode && this.data?.user) {
      const updateRequest: any = { email, role };
      if (password) {
        updateRequest.password = password;
      }

      this.authService.updateUser(this.data.user.id, updateRequest).subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error) => {
          console.error("Failed to update user:", error);
          this.isSaving.set(false);
        },
      });
    } else {
      this.authService.createUser({ email, password, role }).subscribe({
        next: () => {
          this.dialogRef.close(true);
        },
        error: (error) => {
          console.error("Failed to create user:", error);
          this.isSaving.set(false);
        },
      });
    }
  }
}

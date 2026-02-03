import { Component, inject, signal, OnInit } from "@angular/core";
import { CommonModule } from "@angular/common";
import { MatCardModule } from "@angular/material/card";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTableModule } from "@angular/material/table";
import { MatTabsModule } from "@angular/material/tabs";
import { MatDialogModule, MatDialog } from "@angular/material/dialog";
import { MatMenuModule } from "@angular/material/menu";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatChipsModule } from "@angular/material/chips";
import { MatTooltipModule } from "@angular/material/tooltip";
import { MatPaginatorModule, PageEvent } from "@angular/material/paginator";
import { AuthService } from "../../core/services/auth.service";
import { UserDto, GuestLinkDto, PagedResult } from "../../core/models";
import { UserRole } from "../../core/models/enums.model";
import { CreateUserDialogComponent } from "./create-user-dialog.component";
import { CreateGuestLinkDialogComponent } from "./create-guest-link-dialog.component";

@Component({
  selector: "app-users",
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatTabsModule,
    MatDialogModule,
    MatMenuModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatChipsModule,
    MatTooltipModule,
    MatPaginatorModule,
  ],
  template: `
    <div class="users-container">
      <div class="header">
        <h1>Users & Access</h1>
      </div>

      <mat-tab-group>
        <!-- Users Tab -->
        <mat-tab label="Users">
          <div class="tab-content">
            <div class="tab-header">
              <button
                mat-raised-button
                color="primary"
                (click)="openCreateUserDialog()"
              >
                <mat-icon>person_add</mat-icon>
                Add User
              </button>
            </div>

            @if (isLoadingUsers()) {
            <div class="loading">
              <mat-spinner diameter="40"></mat-spinner>
            </div>
            } @else if (users().length === 0) {
            <mat-card class="empty-state">
              <mat-card-content>
                <mat-icon>group</mat-icon>
                <h2>No users found</h2>
                <p>Create users to give access to LibraFoto.</p>
              </mat-card-content>
            </mat-card>
            } @else {
            <mat-card>
              <table mat-table [dataSource]="users()" class="users-table">
                <ng-container matColumnDef="email">
                  <th mat-header-cell *matHeaderCellDef>Email</th>
                  <td mat-cell *matCellDef="let user">
                    <span class="email">{{ user.email }}</span>
                    @if (user.id === currentUser()?.id) {
                    <span class="you-badge">(you)</span>
                    }
                  </td>
                </ng-container>

                <ng-container matColumnDef="role">
                  <th mat-header-cell *matHeaderCellDef>Role</th>
                  <td mat-cell *matCellDef="let user">
                    <span class="role-badge" [class]="getRoleClass(user.role)">
                      {{ getRoleName(user.role) }}
                    </span>
                  </td>
                </ng-container>

                <ng-container matColumnDef="lastLogin">
                  <th mat-header-cell *matHeaderCellDef>Last Login</th>
                  <td mat-cell *matCellDef="let user">
                    {{
                      user.lastLoginAt ? formatDate(user.lastLoginAt) : "Never"
                    }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="actions">
                  <th mat-header-cell *matHeaderCellDef></th>
                  <td mat-cell *matCellDef="let user">
                    <button
                      mat-icon-button
                      [matMenuTriggerFor]="userMenu"
                      [disabled]="user.id === currentUser()?.id"
                    >
                      <mat-icon>more_vert</mat-icon>
                    </button>
                    <mat-menu #userMenu="matMenu">
                      <button mat-menu-item (click)="openEditUserDialog(user)">
                        <mat-icon>edit</mat-icon>
                        Edit
                      </button>
                      <button
                        mat-menu-item
                        (click)="deleteUser(user)"
                        class="delete-action"
                      >
                        <mat-icon>delete</mat-icon>
                        Delete
                      </button>
                    </mat-menu>
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="userColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: userColumns"></tr>
              </table>
              <mat-paginator
                [length]="usersTotalCount()"
                [pageSize]="usersPageSize"
                [pageSizeOptions]="[10, 20, 50]"
                (page)="onUsersPageChange($event)"
              ></mat-paginator>
            </mat-card>
            }

            <mat-card class="info-card">
              <mat-card-header>
                <mat-icon mat-card-avatar>info</mat-icon>
                <mat-card-title>User Roles</mat-card-title>
              </mat-card-header>
              <mat-card-content>
                <ul class="role-info">
                  <li>
                    <strong>Admin:</strong> Full access to all features
                    including user management
                  </li>
                  <li>
                    <strong>Editor:</strong> Can manage photos, albums, tags,
                    and display settings
                  </li>
                  <li>
                    <strong>Guest:</strong> View-only access, can upload via
                    guest links
                  </li>
                </ul>
              </mat-card-content>
            </mat-card>
          </div>
        </mat-tab>

        <!-- Guest Links Tab -->
        <mat-tab label="Guest Links">
          <div class="tab-content">
            <div class="tab-header">
              <button
                mat-raised-button
                color="primary"
                (click)="openCreateGuestLinkDialog()"
              >
                <mat-icon>add_link</mat-icon>
                Create Guest Link
              </button>
            </div>

            @if (isLoadingGuestLinks()) {
            <div class="loading">
              <mat-spinner diameter="40"></mat-spinner>
            </div>
            } @else if (guestLinks().length === 0) {
            <mat-card class="empty-state">
              <mat-card-content>
                <mat-icon>link</mat-icon>
                <h2>No guest links</h2>
                <p>
                  Create guest links to allow others to upload photos without an
                  account.
                </p>
                <button
                  mat-raised-button
                  color="primary"
                  (click)="openCreateGuestLinkDialog()"
                >
                  <mat-icon>add_link</mat-icon>
                  Create Guest Link
                </button>
              </mat-card-content>
            </mat-card>
            } @else {
            <div class="guest-links-grid">
              @for (link of guestLinks(); track link.id) {
              <mat-card [class.inactive]="!link.isActive">
                <mat-card-header>
                  <mat-icon mat-card-avatar [class.inactive]="!link.isActive">
                    {{ link.isActive ? "link" : "link_off" }}
                  </mat-icon>
                  <mat-card-title>{{ link.name }}</mat-card-title>
                  <mat-card-subtitle>
                    @if (link.isActive) {
                    <span class="status active">Active</span>
                    } @else {
                    <span class="status inactive">Inactive</span>
                    }
                  </mat-card-subtitle>
                </mat-card-header>
                <mat-card-content>
                  <div class="link-details">
                    <div class="detail">
                      <mat-icon>upload</mat-icon>
                      <span>{{ link.currentUploads }} uploads</span>
                      @if (link.maxUploads) {
                      <span class="limit">/ {{ link.maxUploads }} max</span>
                      }
                    </div>
                    @if (link.targetAlbumName) {
                    <div class="detail">
                      <mat-icon>photo_album</mat-icon>
                      <span>To: {{ link.targetAlbumName }}</span>
                    </div>
                    } @if (link.expiresAt) {
                    <div
                      class="detail"
                      [class.expired]="isExpired(link.expiresAt)"
                    >
                      <mat-icon>schedule</mat-icon>
                      <span
                        >{{
                          isExpired(link.expiresAt) ? "Expired" : "Expires"
                        }}: {{ formatDate(link.expiresAt) }}</span
                      >
                    </div>
                    }
                    <div class="detail">
                      <mat-icon>person</mat-icon>
                      <span>Created by {{ link.createdByUsername }}</span>
                    </div>
                  </div>

                  <div class="link-code">
                    <code>{{ getGuestLinkUrl(link.id) }}</code>
                    <button
                      mat-icon-button
                      matTooltip="Copy link"
                      (click)="copyGuestLink(link.id)"
                    >
                      <mat-icon>content_copy</mat-icon>
                    </button>
                  </div>
                </mat-card-content>
                <mat-card-actions>
                  <button mat-button (click)="copyGuestLink(link.id)">
                    <mat-icon>share</mat-icon>
                    Copy Link
                  </button>
                  <button mat-icon-button [matMenuTriggerFor]="linkMenu">
                    <mat-icon>more_vert</mat-icon>
                  </button>
                  <mat-menu #linkMenu="matMenu">
                    <button
                      mat-menu-item
                      (click)="deleteGuestLink(link)"
                      class="delete-action"
                    >
                      <mat-icon>delete</mat-icon>
                      Delete
                    </button>
                  </mat-menu>
                </mat-card-actions>
              </mat-card>
              }
            </div>
            <mat-paginator
              [length]="guestLinksTotalCount()"
              [pageSize]="guestLinksPageSize"
              [pageSizeOptions]="[10, 20, 50]"
              (page)="onGuestLinksPageChange($event)"
            ></mat-paginator>
            }
          </div>
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: [
    `
      .users-container {
        padding: 24px;
      }

      .header {
        margin-bottom: 24px;
      }

      .tab-content {
        padding: 24px 0;
      }

      .tab-header {
        display: flex;
        justify-content: flex-end;
        margin-bottom: 16px;
      }

      .loading {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      .empty-state {
        text-align: center;
        padding: 48px;
      }

      .empty-state mat-icon {
        font-size: 64px;
        width: 64px;
        height: 64px;
        color: rgba(0, 0, 0, 0.3);
      }

      .empty-state h2 {
        margin: 16px 0 8px;
      }

      .empty-state p {
        color: rgba(0, 0, 0, 0.6);
        margin-bottom: 24px;
      }

      .users-table {
        width: 100%;
      }

      .email {
        font-weight: 500;
      }

      .you-badge {
        color: #1976d2;
        font-size: 12px;
        margin-left: 8px;
      }

      .role-badge {
        display: inline-block;
        padding: 4px 12px;
        border-radius: 12px;
        font-size: 12px;
        font-weight: 500;
      }

      .role-badge.admin {
        background-color: #ffebee;
        color: #c62828;
      }

      .role-badge.editor {
        background-color: #e3f2fd;
        color: #1565c0;
      }

      .role-badge.guest {
        background-color: #f5f5f5;
        color: #616161;
      }

      .info-card {
        margin-top: 24px;
      }

      .info-card mat-icon {
        color: #1976d2;
      }

      .role-info {
        list-style: none;
        padding: 0;
        margin: 0;
      }

      .role-info li {
        padding: 12px 0;
        border-bottom: 1px solid rgba(0, 0, 0, 0.08);
      }

      .role-info li:last-child {
        border-bottom: none;
      }

      .delete-action {
        color: #f44336;
      }

      .guest-links-grid {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
        gap: 16px;
      }

      mat-card.inactive {
        opacity: 0.7;
      }

      mat-card-header mat-icon.inactive {
        color: #9e9e9e;
      }

      .status.active {
        color: #4caf50;
      }

      .status.inactive {
        color: #9e9e9e;
      }

      .link-details {
        display: flex;
        flex-direction: column;
        gap: 8px;
        margin-bottom: 16px;
      }

      .detail {
        display: flex;
        align-items: center;
        gap: 8px;
        font-size: 13px;
        color: rgba(0, 0, 0, 0.6);
      }

      .detail mat-icon {
        font-size: 18px;
        width: 18px;
        height: 18px;
      }

      .detail .limit {
        color: rgba(0, 0, 0, 0.4);
      }

      .detail.expired {
        color: #f44336;
      }

      .link-code {
        display: flex;
        align-items: center;
        gap: 8px;
        background-color: #f5f5f5;
        padding: 8px 12px;
        border-radius: 4px;
        overflow: hidden;
      }

      .link-code code {
        flex: 1;
        font-size: 12px;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      mat-card-actions {
        display: flex;
        justify-content: space-between;
      }
    `,
  ],
})
export class UsersComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  users = signal<UserDto[]>([]);
  usersTotalCount = signal(0);
  usersPage = 1;
  usersPageSize = 20;
  isLoadingUsers = signal(true);

  guestLinks = signal<GuestLinkDto[]>([]);
  guestLinksTotalCount = signal(0);
  guestLinksPage = 1;
  guestLinksPageSize = 20;
  isLoadingGuestLinks = signal(true);

  currentUser = this.authService.currentUser;

  userColumns = ["email", "role", "lastLogin", "actions"];

  ngOnInit(): void {
    this.loadUsers();
    this.loadGuestLinks();
  }

  loadUsers(): void {
    this.isLoadingUsers.set(true);
    this.authService.getUsers(this.usersPage, this.usersPageSize).subscribe({
      next: (result) => {
        this.users.set(result.data);
        this.usersTotalCount.set(result.pagination.totalItems);
        this.isLoadingUsers.set(false);
      },
      error: (error) => {
        console.error("Failed to load users:", error);
        this.isLoadingUsers.set(false);
        this.snackBar.open("Failed to load users", "Close", { duration: 3000 });
      },
    });
  }

  loadGuestLinks(): void {
    this.isLoadingGuestLinks.set(true);
    this.authService
      .getGuestLinks(this.guestLinksPage, this.guestLinksPageSize)
      .subscribe({
        next: (result) => {
          this.guestLinks.set(result.data);
          this.guestLinksTotalCount.set(result.pagination.totalItems);
          this.isLoadingGuestLinks.set(false);
        },
        error: (error) => {
          console.error("Failed to load guest links:", error);
          this.isLoadingGuestLinks.set(false);
          this.snackBar.open("Failed to load guest links", "Close", {
            duration: 3000,
          });
        },
      });
  }

  onUsersPageChange(event: PageEvent): void {
    this.usersPage = event.pageIndex + 1;
    this.usersPageSize = event.pageSize;
    this.loadUsers();
  }

  onGuestLinksPageChange(event: PageEvent): void {
    this.guestLinksPage = event.pageIndex + 1;
    this.guestLinksPageSize = event.pageSize;
    this.loadGuestLinks();
  }

  getRoleName(role: UserRole): string {
    switch (role) {
      case UserRole.Admin:
        return "Admin";
      case UserRole.Editor:
        return "Editor";
      case UserRole.Guest:
        return "Guest";
      default:
        return "Unknown";
    }
  }

  getRoleClass(role: UserRole): string {
    switch (role) {
      case UserRole.Admin:
        return "admin";
      case UserRole.Editor:
        return "editor";
      case UserRole.Guest:
        return "guest";
      default:
        return "";
    }
  }

  formatDate(dateString: string): string {
    const date = new Date(dateString);
    return (
      date.toLocaleDateString() +
      " " +
      date.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })
    );
  }

  isExpired(dateString: string): boolean {
    return new Date(dateString) < new Date();
  }

  getGuestLinkUrl(linkCode: string): string {
    return `${window.location.origin}/guest/${linkCode}`;
  }

  copyGuestLink(linkCode: string): void {
    const url = this.getGuestLinkUrl(linkCode);
    navigator.clipboard.writeText(url).then(() => {
      this.snackBar.open("Link copied to clipboard", "Close", {
        duration: 2000,
      });
    });
  }

  openCreateUserDialog(): void {
    const dialogRef = this.dialog.open(CreateUserDialogComponent, {
      width: "400px",
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadUsers();
      }
    });
  }

  openEditUserDialog(user: UserDto): void {
    const dialogRef = this.dialog.open(CreateUserDialogComponent, {
      width: "400px",
      data: { user },
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadUsers();
      }
    });
  }

  deleteUser(user: UserDto): void {
    if (confirm(`Delete user "${user.email}"? This action cannot be undone.`)) {
      this.authService.deleteUser(user.id).subscribe({
        next: () => {
          this.snackBar.open("User deleted", "Close", { duration: 3000 });
          this.loadUsers();
        },
        error: (error) => {
          console.error("Failed to delete user:", error);
          this.snackBar.open("Failed to delete user", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }

  openCreateGuestLinkDialog(): void {
    const dialogRef = this.dialog.open(CreateGuestLinkDialogComponent, {
      width: "450px",
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.loadGuestLinks();
      }
    });
  }

  deleteGuestLink(link: GuestLinkDto): void {
    if (
      confirm(
        `Delete guest link "${link.name}"? This will invalidate the link.`
      )
    ) {
      this.authService.deleteGuestLink(link.id).subscribe({
        next: () => {
          this.snackBar.open("Guest link deleted", "Close", { duration: 3000 });
          this.loadGuestLinks();
        },
        error: (error) => {
          console.error("Failed to delete guest link:", error);
          this.snackBar.open("Failed to delete guest link", "Close", {
            duration: 3000,
          });
        },
      });
    }
  }
}

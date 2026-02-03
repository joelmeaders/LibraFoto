import { Component, inject } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { RouterOutlet, RouterLink, RouterLinkActive } from "@angular/router";
import { MatToolbarModule } from "@angular/material/toolbar";
import { MatSidenavModule } from "@angular/material/sidenav";
import { MatListModule } from "@angular/material/list";
import { MatIconModule } from "@angular/material/icon";
import { MatButtonModule } from "@angular/material/button";
import { BreakpointObserver, Breakpoints } from "@angular/cdk/layout";
import { map } from "rxjs/operators";

@Component({
  selector: "app-root",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
  ],
  template: `
    <mat-toolbar color="primary" class="mat-elevation-z4 app-toolbar">
      <button mat-icon-button (click)="sidenav.toggle()">
        <mat-icon>menu</mat-icon>
      </button>
      <span class="brand-logo">LibraFoto Admin</span>
      <span class="spacer"></span>
      <button mat-icon-button>
        <mat-icon>account_circle</mat-icon>
      </button>
    </mat-toolbar>

    <mat-sidenav-container class="sidenav-container">
      <mat-sidenav
        #sidenav
        class="sidenav"
        [mode]="isHandset() ? 'over' : 'side'"
        [opened]="!isHandset()"
      >
        <mat-nav-list>
          <a
            mat-list-item
            routerLink="/dashboard"
            routerLinkActive="active"
            (click)="isHandset() && sidenav.close()"
          >
            <mat-icon matListItemIcon>dashboard</mat-icon>
            <span matListItemTitle>Dashboard</span>
          </a>
          <a
            mat-list-item
            routerLink="/photos"
            routerLinkActive="active"
            (click)="isHandset() && sidenav.close()"
          >
            <mat-icon matListItemIcon>photo_library</mat-icon>
            <span matListItemTitle>Photos</span>
          </a>
          <a
            mat-list-item
            routerLink="/albums"
            routerLinkActive="active"
            (click)="isHandset() && sidenav.close()"
          >
            <mat-icon matListItemIcon>collections</mat-icon>
            <span matListItemTitle>Albums</span>
          </a>
          <a
            mat-list-item
            routerLink="/tags"
            routerLinkActive="active"
            (click)="isHandset() && sidenav.close()"
          >
            <mat-icon matListItemIcon>label</mat-icon>
            <span matListItemTitle>Tags</span>
          </a>
          <mat-divider></mat-divider>
          <a
            mat-list-item
            routerLink="/display"
            routerLinkActive="active"
            (click)="isHandset() && sidenav.close()"
          >
            <mat-icon matListItemIcon>tv</mat-icon>
            <span matListItemTitle>Display Settings</span>
          </a>
          <a
            mat-list-item
            routerLink="/storage"
            routerLinkActive="active"
            (click)="isHandset() && sidenav.close()"
          >
            <mat-icon matListItemIcon>cloud</mat-icon>
            <span matListItemTitle>Storage</span>
          </a>
          <a
            mat-list-item
            routerLink="/users"
            routerLinkActive="active"
            (click)="isHandset() && sidenav.close()"
          >
            <mat-icon matListItemIcon>people</mat-icon>
            <span matListItemTitle>Users</span>
          </a>
        </mat-nav-list>
      </mat-sidenav>

      <mat-sidenav-content class="content">
        <router-outlet></router-outlet>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [
    `
      .app-toolbar {
        position: relative;
        z-index: 2;
      }

      .brand-logo {
        font-family: 'Poppins', sans-serif;
        font-weight: 500;
        letter-spacing: 0.5px;
        margin-left: 8px;
      }

      .sidenav-container {
        height: calc(100vh - 64px);
      }

      .sidenav {
        width: 260px;
        border-right: none;
        box-shadow: 1px 0 0 rgba(0, 0, 0, 0.05);
      }

      .content {
        padding: 0;
        background-color: #f4f6f8;
      }

      .spacer {
        flex: 1 1 auto;
      }

      .active {
        background-color: rgba(103, 58, 183, 0.08); /* Deep purple with low opacity */
        color: #673ab7;
        border-right: 3px solid #673ab7;
      }

      .active mat-icon {
        color: #673ab7;
      }

      mat-divider {
        margin: 8px 16px;
        opacity: 0.6;
      }
      
      mat-nav-list a {
        border-radius: 0 24px 24px 0;
        margin-right: 12px;
      }
    `,
  ],
})
export class AppComponent {
  title = "LibraFoto Admin";
  private breakpointObserver = inject(BreakpointObserver);

  isHandset = toSignal(
    this.breakpointObserver
      .observe(Breakpoints.Handset)
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );
}

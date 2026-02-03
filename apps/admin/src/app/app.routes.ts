import { Routes } from "@angular/router";
import {
  authGuard,
  adminGuard,
  editorGuard,
  setupPageGuard,
  noAuthGuard,
} from "./core/guards/auth.guard";

export const routes: Routes = [
  {
    path: "",
    redirectTo: "dashboard",
    pathMatch: "full",
  },
  {
    path: "setup",
    loadComponent: () =>
      import("./features/setup/setup.component").then((m) => m.SetupComponent),
    canActivate: [setupPageGuard],
  },
  {
    path: "login",
    loadComponent: () =>
      import("./features/auth/login.component").then((m) => m.LoginComponent),
    canActivate: [noAuthGuard],
  },
  {
    path: "oauth/callback",
    loadComponent: () =>
      import("./features/auth/oauth-callback.component").then(
        (m) => m.OAuthCallbackComponent
      ),
    // No guard - OAuth callback needs to work for any user state
  },
  {
    path: "dashboard",
    loadComponent: () =>
      import("./features/dashboard/dashboard.component").then(
        (m) => m.DashboardComponent
      ),
    canActivate: [authGuard],
  },
  {
    path: "photos",
    loadComponent: () =>
      import("./features/photos/photos.component").then(
        (m) => m.PhotosComponent
      ),
    canActivate: [authGuard],
  },
  {
    path: "albums",
    loadComponent: () =>
      import("./features/albums/albums.component").then(
        (m) => m.AlbumsComponent
      ),
    canActivate: [authGuard],
  },
  {
    path: "tags",
    loadComponent: () =>
      import("./features/tags/tags.component").then((m) => m.TagsComponent),
    canActivate: [authGuard],
  },
  {
    path: "storage",
    loadComponent: () =>
      import("./features/storage/storage.component").then(
        (m) => m.StorageComponent
      ),
    canActivate: [editorGuard],
  },
  {
    path: "display",
    loadComponent: () =>
      import("./features/display-settings/display-settings.component").then(
        (m) => m.DisplaySettingsComponent
      ),
    canActivate: [editorGuard],
  },
  {
    path: "users",
    loadComponent: () =>
      import("./features/users/users.component").then((m) => m.UsersComponent),
    canActivate: [adminGuard],
  },
  {
    path: "**",
    redirectTo: "dashboard",
  },
];

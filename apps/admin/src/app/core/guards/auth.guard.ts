import { inject } from "@angular/core";
import { Router, type CanActivateFn } from "@angular/router";
import { AuthService } from "../services/auth.service";
import { UserRole } from "../models";
import { firstValueFrom } from "rxjs";

/**
 * Guard that checks if the user is authenticated.
 * Redirects to login if not authenticated.
 */
export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  // Store the attempted URL for redirecting after login
  router.navigate(["/login"]);
  return false;
};

/**
 * Guard that checks if the user has admin role.
 * Redirects to dashboard if not admin.
 */
export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(["/login"]);
    return false;
  }

  if (authService.isAdmin()) {
    return true;
  }

  // User is authenticated but not admin
  router.navigate(["/dashboard"]);
  return false;
};

/**
 * Guard that checks if the user has editor or admin role.
 * Redirects to dashboard if not editor or admin.
 */
export const editorGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    router.navigate(["/login"]);
    return false;
  }

  if (authService.isEditor()) {
    return true;
  }

  // User is authenticated but doesn't have edit permissions
  router.navigate(["/dashboard"]);
  return false;
};

/**
 * Guard that checks if setup is required.
 * Redirects to setup if no admin user exists.
 */
export const setupGuard: CanActivateFn = async () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  try {
    const status = await firstValueFrom(authService.checkSetupStatus());

    if (status?.isSetupRequired) {
      router.navigate(["/setup"]);
      return false;
    }

    return true;
  } catch {
    // If we can't check setup status, allow access
    return true;
  }
};

/**
 * Guard for the setup page - only accessible if setup is required.
 */
export const setupPageGuard: CanActivateFn = async () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  try {
    const status = await firstValueFrom(authService.checkSetupStatus());

    if (status?.isSetupRequired) {
      return true;
    }

    // Setup not required, redirect to login
    router.navigate(["/login"]);
    return false;
  } catch {
    // If we can't check, redirect to login
    router.navigate(["/login"]);
    return false;
  }
};

/**
 * Guard that allows access only if user is NOT authenticated.
 * Used for login page to redirect authenticated users.
 */
export const noAuthGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    router.navigate(["/dashboard"]);
    return false;
  }

  return true;
};

/**
 * Guard that checks for a specific role.
 * Factory function to create role-specific guards.
 */
export function roleGuard(requiredRole: UserRole): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated()) {
      router.navigate(["/login"]);
      return false;
    }

    const currentUser = authService.currentUser();
    if (!currentUser || currentUser.role < requiredRole) {
      router.navigate(["/dashboard"]);
      return false;
    }

    return true;
  };
}

import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpErrorResponse,
} from "@angular/common/http";
import { inject } from "@angular/core";
import { catchError, switchMap, throwError } from "rxjs";
import { AuthService } from "../services/auth.service";

/**
 * HTTP interceptor that attaches JWT tokens to outgoing requests
 * and handles token refresh on 401 responses.
 */
export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const authService = inject(AuthService);

  // Skip auth header for certain endpoints
  const skipUrls = [
    "/api/auth/login",
    "/api/setup/complete",
    "/api/setup/status",
    "/api/auth/refresh",
    "/api/guest/",
    "/api/guest/upload/",
  ];

  const shouldSkip = skipUrls.some((url) => req.url.includes(url));

  if (shouldSkip) {
    return next(req);
  }

  // Add auth token if available
  const token = authService.getToken();
  let authReq = req;

  if (token) {
    authReq = addToken(req, token);
  }

  return next(authReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !req.url.includes("/api/auth/refresh")) {
        // Token might be expired, try to refresh
        return handleTokenRefresh(authService, req, next);
      }
      return throwError(() => error);
    })
  );
};

/**
 * Add authorization header to request.
 */
function addToken(
  req: HttpRequest<unknown>,
  token: string
): HttpRequest<unknown> {
  return req.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`,
    },
  });
}

/**
 * Handle token refresh and retry the original request.
 */
function handleTokenRefresh(
  authService: AuthService,
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) {
  // Check if we have a refresh token
  const refreshToken = authService.getRefreshToken();

  if (!refreshToken) {
    authService.logout();
    return throwError(
      () => new HttpErrorResponse({ status: 401, statusText: "Unauthorized" })
    );
  }

  return authService.refreshToken().pipe(
    switchMap((response) => {
      if (response && response.token) {
        // Retry the original request with new token
        return next(addToken(req, response.token));
      }
      authService.logout();
      return throwError(
        () => new HttpErrorResponse({ status: 401, statusText: "Unauthorized" })
      );
    }),
    catchError((error) => {
      authService.logout();
      return throwError(() => error);
    })
  );
}

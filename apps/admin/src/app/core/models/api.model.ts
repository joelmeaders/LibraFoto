/**
 * Standard error response format for API errors.
 */
export interface ApiError {
  code: string;
  message: string;
  details?: unknown;
}

/**
 * Pagination metadata for paginated API responses.
 */
export interface PaginationInfo {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

/**
 * Generic wrapper for paginated API responses.
 */
export interface PagedResult<T> {
  data: T[];
  pagination: PaginationInfo;
}

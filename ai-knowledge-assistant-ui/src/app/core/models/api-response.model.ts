export interface ApiResponse<T> {
  data: T;
  message?: string;
  correlationId?: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  email: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface UserDto {
  id: string;
  email: string;
  role: string;
}

export interface StoredAuthState {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  refreshTokenExpiresAt: string;
  user: UserDto;
}

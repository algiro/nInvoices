import axios, { type AxiosInstance, AxiosError } from 'axios';
import { useAuthStore } from '../stores/auth';

/**
 * API Client configuration
 * Follows Single Responsibility Principle - handles HTTP communication
 * Includes JWT token injection for authenticated requests
 */
class ApiClient {
  private client: AxiosInstance;

  constructor(baseURL: string = import.meta.env.VITE_API_URL || '') {
    // Use empty baseURL to leverage Vite proxy in development
    const finalBaseURL = baseURL || '';
    console.log('[API Client] Base URL:', finalBaseURL || '(empty - using Vite proxy)');
    
    this.client = axios.create({
      baseURL: finalBaseURL,
      headers: {
        'Content-Type': 'application/json',
      },
      timeout: 30000,
    });

    this.setupInterceptors();
  }

  private setupInterceptors(): void {
    // Request interceptor - add JWT token
    this.client.interceptors.request.use(
      async (config) => {
        // Get the auth store
        const authStore = useAuthStore();
        
        // Get access token
        const token = await authStore.getAccessToken();
        
        console.log('[API Client] Request to:', config.url);
        console.log('[API Client] Token available:', !!token);
        if (token) {
          console.log('[API Client] Token (first 50 chars):', token.substring(0, 50) + '...');
        }
        
        // Add token to Authorization header if available
        if (token) {
          config.headers.Authorization = `Bearer ${token}`;
        }
        
        return config;
      },
      (error) => Promise.reject(error)
    );

    // Response interceptor - handle errors
    this.client.interceptors.response.use(
      (response) => response,
      async (error: AxiosError) => {
        if (error.response) {
          console.error('API Error:', error.response.status, error.response.data);
          
          // Handle 401 Unauthorized - token expired or invalid
          if (error.response.status === 401) {
            const authStore = useAuthStore();
            
            // Try to refresh user info
            await authStore.refreshUser();
            
            // If still not authenticated, redirect to login
            if (!authStore.isAuthenticated) {
              console.warn('User not authenticated, redirecting to login...');
              await authStore.login();
            }
          }
        } else if (error.request) {
          console.error('Network Error:', error.message);
        } else {
          console.error('Request Error:', error.message);
        }
        return Promise.reject(error);
      }
    );
  }

  async get<T>(url: string, params?: any): Promise<T> {
    const response = await this.client.get<T>(url, { params });
    return response.data;
  }

  async post<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.post<T>(url, data);
    return response.data;
  }

  async put<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.put<T>(url, data);
    return response.data;
  }

  async delete<T>(url: string, params?: any): Promise<T> {
    const response = await this.client.delete<T>(url, { params });
    return response.data;
  }

  async downloadFile(url: string): Promise<Blob> {
    const response = await this.client.get(url, {
      responseType: 'blob',
    });
    return response.data;
  }
}

export const apiClient = new ApiClient();


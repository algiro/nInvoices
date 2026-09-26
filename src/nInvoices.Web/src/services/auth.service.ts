import { UserManager, User, UserManagerSettings } from 'oidc-client-ts';

// Get the base path from Vite's build-time configuration
const basePath = import.meta.env.BASE_URL.endsWith('/') 
  ? import.meta.env.BASE_URL.slice(0, -1) 
  : import.meta.env.BASE_URL;

const keycloakConfig: UserManagerSettings = {
  authority: import.meta.env.VITE_KEYCLOAK_URL + '/realms/' + import.meta.env.VITE_KEYCLOAK_REALM,
  client_id: import.meta.env.VITE_KEYCLOAK_CLIENT_ID,
  redirect_uri: window.location.origin + basePath + '/auth/callback',
  post_logout_redirect_uri: window.location.origin + basePath,
  response_type: 'code',
  scope: 'openid profile email',
  automaticSilentRenew: true,
  silent_redirect_uri: window.location.origin + basePath + '/auth/silent-callback',
  loadUserInfo: true,
  metadata: {
    issuer: import.meta.env.VITE_KEYCLOAK_URL + '/realms/' + import.meta.env.VITE_KEYCLOAK_REALM,
    authorization_endpoint: import.meta.env.VITE_KEYCLOAK_URL + '/realms/' + import.meta.env.VITE_KEYCLOAK_REALM + '/protocol/openid-connect/auth',
    token_endpoint: import.meta.env.VITE_KEYCLOAK_URL + '/realms/' + import.meta.env.VITE_KEYCLOAK_REALM + '/protocol/openid-connect/token',
    userinfo_endpoint: import.meta.env.VITE_KEYCLOAK_URL + '/realms/' + import.meta.env.VITE_KEYCLOAK_REALM + '/protocol/openid-connect/userinfo',
    end_session_endpoint: import.meta.env.VITE_KEYCLOAK_URL + '/realms/' + import.meta.env.VITE_KEYCLOAK_REALM + '/protocol/openid-connect/logout',
    jwks_uri: import.meta.env.VITE_KEYCLOAK_URL + '/realms/' + import.meta.env.VITE_KEYCLOAK_REALM + '/protocol/openid-connect/certs'
  }
};

class AuthService {
  private userManager: UserManager;

  constructor() {
    this.userManager = new UserManager(keycloakConfig);
    
    // Setup automatic token renewal
    this.userManager.events.addUserLoaded((user: User) => {
      console.log('User loaded:', user.profile.sub);
    });
    
    this.userManager.events.addAccessTokenExpiring(() => {
      console.log('Access token expiring, renewing...');
      this.userManager.signinSilent().catch(err => {
        console.error('Silent renew failed:', err);
      });
    });
    
    this.userManager.events.addAccessTokenExpired(() => {
      console.log('Access token expired');
    });
    
    this.userManager.events.addSilentRenewError((err) => {
      console.error('Silent renew error:', err);
    });
  }

  /**
   * Initiates the login flow by redirecting to Keycloak
   */
  async login(): Promise<void> {
    try {
      await this.userManager.signinRedirect();
    } catch (err) {
      console.error('[Auth Service] Login error:', err);
      throw err;
    }
  }

  /**
   * Handles the callback after successful authentication
   */
  async handleCallback(): Promise<User> {
    const user = await this.userManager.signinRedirectCallback();
    return user;
  }

  /**
   * Logs out the current user
   */
  async logout(): Promise<void> {
    await this.userManager.signoutRedirect();
  }

  /**
   * Gets the current authenticated user
   */
  async getUser(): Promise<User | null> {
    return await this.userManager.getUser();
  }

  /**
   * Gets the access token for API calls
   */
  async getAccessToken(): Promise<string | null> {
    const user = await this.getUser();
    return user?.access_token || null;
  }

  /**
   * Checks if the user is authenticated
   */
  async isAuthenticated(): Promise<boolean> {
    const user = await this.getUser();
    return user !== null && !user.expired;
  }

  /**
   * Refreshes the access token silently
   */
  async renewToken(): Promise<User | null> {
    return await this.userManager.signinSilent();
  }

  /**
   * Checks if user has a specific role
   */
  async hasRole(role: string): Promise<boolean> {
    const user = await this.getUser();
    if (!user) return false;
    
    const roles = (user.profile as any).realm_access?.roles || [];
    return roles.includes(role);
  }

  /**
   * Gets user profile information
   */
  async getUserProfile(): Promise<{
    sub: string;
    email?: string;
    name?: string;
    preferred_username?: string;
    roles: string[];
  } | null> {
    const user = await this.getUser();
    if (!user) return null;
    
    return {
      sub: user.profile.sub,
      email: user.profile.email,
      name: user.profile.name,
      preferred_username: user.profile.preferred_username,
      roles: (user.profile as any).realm_access?.roles || []
    };
  }
}

export const authService = new AuthService();
export default authService;

import { apiClient } from './client';

/** What the user types to confirm deleting their account (checked by the API too). */
export const DELETE_ACCOUNT_CONFIRMATION = 'DELETE';

export interface DeleteAccountResult {
  deletedRows: number;
}

export interface AccessRequestResult {
  approved: boolean;
  /** The administrator was told (now or earlier) that this account waits for approval. */
  administratorNotified: boolean;
}

export const accountApi = {
  /** Asks the administrator for access; they are notified once per account. */
  async requestAccess(): Promise<AccessRequestResult> {
    return apiClient.post<AccessRequestResult>('/api/access-requests');
  },

  /** Deletes all of the signed-in user's data on this server. It can't be undone. */
  async deleteAccount(confirmation: string): Promise<DeleteAccountResult> {
    return apiClient.deleteWithBody<DeleteAccountResult>('/api/account', { confirmation });
  },
};

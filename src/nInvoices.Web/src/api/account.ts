import { apiClient } from './client';

/** What the user types to confirm deleting their account (checked by the API too). */
export const DELETE_ACCOUNT_CONFIRMATION = 'DELETE';

export interface DeleteAccountResult {
  deletedRows: number;
}

export const accountApi = {
  /** Deletes all of the signed-in user's data on this server. It can't be undone. */
  async deleteAccount(confirmation: string): Promise<DeleteAccountResult> {
    return apiClient.deleteWithBody<DeleteAccountResult>('/api/account', { confirmation });
  },
};

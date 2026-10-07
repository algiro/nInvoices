<template>
  <BasePanel
    title="Delete your account"
    description="Removes everything you have on this server. It can't be undone."
    class="danger-zone"
  >
    <div class="delete-account">
      <p>
        Your customers, invoices, worked days, templates, images, settings and e-invoicing records are
        deleted, and your encryption key is destroyed: copies in the server's backups can no longer be
        read. Then Keycloak asks you to confirm deleting your sign-in account too.
      </p>
      <p class="note warning">
        Tax rules usually require keeping issued invoices for several years (four in Spain, together
        with their Verifactu records). Download a backup above first and keep it: it opens without
        nInvoices.
      </p>
      <label class="toggle" for="deleteHasBackup">
        <input id="deleteHasBackup" v-model="deleteHasBackup" type="checkbox" />
        <span>I have a backup of what I need to keep</span>
      </label>
      <BaseField :label="`Type ${DELETE_ACCOUNT_CONFIRMATION} to confirm`" for="deleteConfirm" :error="deleteError">
        <input id="deleteConfirm" v-model="deleteConfirmText" type="text" class="control" autocomplete="off" spellcheck="false" />
      </BaseField>
      <div class="buttons">
        <BaseButton variant="danger" :loading="deleting" :disabled="!canDeleteAccount" @click="handleDeleteAccount">
          Delete my account
        </BaseButton>
      </div>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { accountApi, DELETE_ACCOUNT_CONFIRMATION } from '@/api/account'
import authService from '@/services/auth.service'
import { errorMessage } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'

const { confirm } = useConfirm()

// Delete account: data on this server first, then Keycloak's own "Delete account" page
const router = useRouter()
const deleteHasBackup = ref(false)
const deleteConfirmText = ref('')
const deleting = ref(false)
const deleteError = ref<string | null>(null)

const canDeleteAccount = computed(() =>
  deleteHasBackup.value && deleteConfirmText.value.trim() === DELETE_ACCOUNT_CONFIRMATION && !deleting.value)

async function handleDeleteAccount() {
  if (!canDeleteAccount.value) return
  const ok = await confirm({
    title: 'Delete your account?',
    message: 'All your data on this server is deleted now, and it cannot be recovered.',
    confirmLabel: 'Delete my account',
    tone: 'danger',
  })
  if (!ok) return

  deleting.value = true
  deleteError.value = null
  try {
    await accountApi.deleteAccount(deleteConfirmText.value.trim())
    if (import.meta.env.VITE_AUTH_DISABLED === 'true') {
      // No Keycloak in this mode: only the data could be deleted
      await router.replace({ name: 'account-deleted', query: { signin: 'kept' } })
    } else {
      await authService.deleteSignInAccount()
    }
  } catch (error) {
    deleteError.value = errorMessage(error, 'The account could not be deleted.')
  } finally {
    deleting.value = false
  }
}
</script>

<style scoped>
.note {
  margin: 0.75rem 0 0;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.delete-account {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  max-width: 40rem;
}

.delete-account p {
  margin: 0;
  font-size: var(--text-md);
  line-height: 1.55;
}

.delete-account .note.warning {
  color: var(--color-warning);
}

.danger-zone {
  border-color: var(--color-danger-line);
}

.toggle {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: var(--text-md);
  color: var(--color-text-secondary);
  cursor: pointer;
}

.buttons {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>

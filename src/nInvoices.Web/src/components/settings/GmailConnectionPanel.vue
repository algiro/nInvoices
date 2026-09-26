<template>
  <BasePanel
    title="Gmail"
    description="Invoice emails are created as drafts in your Gmail, so they leave from your address, appear in Sent, and replies reach your inbox."
  >
    <LoadingState v-if="loading" label="Checking the Gmail connection…" />

    <EmptyState
      v-else-if="loadError"
      icon="alert"
      title="The Gmail connection could not be checked"
      :description="loadError"
      compact
    >
      <BaseButton @click="load">Try again</BaseButton>
    </EmptyState>

    <p v-else-if="status && !status.configured" class="note">
      Gmail is not set up on the server. Add the Google OAuth client (Gmail:ClientId, Gmail:ClientSecret, Gmail:RedirectUri)
      to the API configuration to turn this on.
    </p>

    <div v-else-if="status" class="connection">
      <div class="state">
        <StatusPill :tone="status.connected ? 'success' : 'neutral'">{{ status.connected ? 'Connected' : 'Not connected' }}</StatusPill>
        <div v-if="status.connected" class="details">
          <strong>{{ status.emailAddress }}</strong>
          <span class="muted">
            since {{ formatDate(status.connectedAt) }}<template v-if="status.lastUsedAt"> · last draft {{ formatDate(status.lastUsedAt) }}</template>
          </span>
        </div>
        <span v-else class="muted">Connect once; the app can then only create drafts, you always send them yourself.</span>
      </div>

      <div class="buttons">
        <BaseButton v-if="status.connected" variant="ghost-danger" :loading="busy === 'disconnect'" :disabled="!!busy" @click="disconnect">
          Disconnect
        </BaseButton>
        <BaseButton :variant="status.connected ? 'secondary' : 'primary'" icon="send" :loading="busy === 'connect'" :disabled="!!busy" @click="connect">
          {{ status.connected ? 'Reconnect' : 'Connect Gmail' }}
        </BaseButton>
      </div>
    </div>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { gmailApi } from '@/api/gmail'
import type { GmailStatusDto } from '@/types'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { useToast } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import { formatDate } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const toast = useToast()
const { confirm } = useConfirm()

const status = ref<GmailStatusDto | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
const busy = ref<'connect' | 'disconnect' | null>(null)

// Reasons the API puts in ?gmail=error&reason=… after Google's redirect
const FAILURE_REASONS: Record<string, string> = {
  access_denied: 'Access was not granted on the Google screen.',
  missing_scope: 'The permission to manage drafts was unticked on the Google screen. Connect again and leave it ticked.',
  expired_state: 'The Google screen was open too long. Please try again.',
  invalid_state: 'The connection request was not recognised. Please try again from this page.',
  exchange_failed: 'Google did not confirm the connection. Please try again.'
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    status.value = await gmailApi.getStatus()
  } catch (error) {
    loadError.value = 'The server did not answer.'
    toast.failure('Could not check the Gmail connection', error)
  } finally {
    loading.value = false
  }
}

async function connect() {
  busy.value = 'connect'
  try {
    const { authorizationUrl } = await gmailApi.connect()
    // Google's consent screen; it redirects to the API, which comes back here with ?gmail=…
    window.location.assign(authorizationUrl)
  } catch (error) {
    toast.failure('Could not start connecting Gmail', error)
    busy.value = null
  }
}

async function disconnect() {
  if (!(await confirm({
    title: 'Disconnect Gmail?',
    message: 'The app will no longer be able to create drafts. Drafts and emails already in Gmail are not touched.',
    confirmLabel: 'Disconnect',
    tone: 'danger'
  }))) return

  busy.value = 'disconnect'
  try {
    await gmailApi.disconnect()
    toast.success('Gmail disconnected')
    await load()
  } catch (error) {
    toast.failure('Could not disconnect Gmail', error)
  } finally {
    busy.value = null
  }
}

/** Shows the outcome of Google's redirect once, then removes it from the URL. */
function reportRedirectOutcome() {
  const outcome = route.query.gmail
  if (typeof outcome !== 'string') return

  if (outcome === 'connected') {
    toast.success('Gmail connected', { message: 'Invoice emails can now be created as Gmail drafts.' })
  } else {
    const reason = typeof route.query.reason === 'string' ? route.query.reason : ''
    toast.error('Gmail was not connected', { message: FAILURE_REASONS[reason] ?? 'Please try again.' })
  }

  const { gmail: _gmail, reason: _reason, ...rest } = route.query
  router.replace({ query: rest })
}

onMounted(() => {
  reportRedirectOutcome()
  load()
})
</script>

<style scoped>
.note {
  margin: 0;
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.connection {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem 1rem;
}

.state {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 0;
}

.details {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.details strong {
  overflow-wrap: anywhere;
}

.muted {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.buttons {
  display: flex;
  gap: 0.5rem;
}
</style>

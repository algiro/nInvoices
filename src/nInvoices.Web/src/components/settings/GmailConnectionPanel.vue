<template>
  <BasePanel
    :title="t('gmail.title')"
    :description="t('gmail.description')"
  >
    <LoadingState v-if="loading" :label="t('gmail.checking')" />

    <EmptyState
      v-else-if="loadError"
      icon="alert"
      :title="t('gmail.checkFailed')"
      :description="loadError"
      compact
    >
      <BaseButton @click="load">{{ t('common.tryAgain') }}</BaseButton>
    </EmptyState>

    <p v-else-if="status && !status.configured" class="note">
      {{ t('gmail.notConfigured') }}
    </p>

    <div v-else-if="status" class="connection">
      <div class="state">
        <StatusPill :tone="status.connected ? 'success' : 'neutral'">{{ status.connected ? t('gmail.connected') : t('gmail.notConnected') }}</StatusPill>
        <div v-if="status.connected" class="details">
          <strong>{{ status.emailAddress }}</strong>
          <span class="muted">
            {{ t('gmail.since', { date: formatDate(status.connectedAt) }) }}<template v-if="status.lastUsedAt"> · {{ t('gmail.lastDraft', { date: formatDate(status.lastUsedAt) }) }}</template>
          </span>
        </div>
        <span v-else class="muted">{{ t('gmail.connectHint') }}</span>
      </div>

      <div class="buttons">
        <BaseButton v-if="status.connected" variant="ghost-danger" :loading="busy === 'disconnect'" :disabled="!!busy" @click="disconnect">
          {{ t('gmail.disconnect') }}
        </BaseButton>
        <BaseButton :variant="status.connected ? 'secondary' : 'primary'" icon="send" :loading="busy === 'connect'" :disabled="!!busy" @click="connect">
          {{ status.connected ? t('gmail.reconnect') : t('gmail.connect') }}
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
import type { MessageKey } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const route = useRoute()
const router = useRouter()
const toast = useToast()
const { confirm } = useConfirm()

const status = ref<GmailStatusDto | null>(null)
const loading = ref(false)
const loadError = ref<string | null>(null)
const busy = ref<'connect' | 'disconnect' | null>(null)

// Reasons the API puts in ?gmail=error&reason=… after Google's redirect
const FAILURE_REASONS: Record<string, MessageKey> = {
  access_denied: 'gmail.reasons.accessDenied',
  missing_scope: 'gmail.reasons.missingScope',
  expired_state: 'gmail.reasons.expiredState',
  invalid_state: 'gmail.reasons.invalidState',
  exchange_failed: 'gmail.reasons.exchangeFailed'
}

async function load() {
  loading.value = true
  loadError.value = null
  try {
    status.value = await gmailApi.getStatus()
  } catch (error) {
    loadError.value = t('gmail.noAnswer')
    toast.failure(t('gmail.checkFailed'), error)
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
    toast.failure(t('gmail.connectFailed'), error)
    busy.value = null
  }
}

async function disconnect() {
  if (!(await confirm({
    title: t('gmail.disconnectTitle'),
    message: t('gmail.disconnectMessage'),
    confirmLabel: t('gmail.disconnect'),
    tone: 'danger'
  }))) return

  busy.value = 'disconnect'
  try {
    await gmailApi.disconnect()
    toast.success(t('gmail.disconnected'))
    await load()
  } catch (error) {
    toast.failure(t('gmail.disconnectFailed'), error)
  } finally {
    busy.value = null
  }
}

/** Shows the outcome of Google's redirect once, then removes it from the URL. */
function reportRedirectOutcome() {
  const outcome = route.query.gmail
  if (typeof outcome !== 'string') return

  if (outcome === 'connected') {
    toast.success(t('gmail.connectedToast'), { message: t('gmail.connectedMessage') })
  } else {
    const reason = typeof route.query.reason === 'string' ? route.query.reason : ''
    toast.error(t('gmail.notConnectedToast'), { message: FAILURE_REASONS[reason] ? t(FAILURE_REASONS[reason]) : t('gmail.tryAgain') })
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

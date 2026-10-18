<template>
  <div class="settings-page">
    <PageHeader :title="t('nav.settings')" />

    <div class="layout">
      <nav class="sub-nav" :aria-label="t('settings.sections')">
        <router-link
          v-for="section in sections"
          :key="section.name"
          :to="{ name: section.name }"
          class="sub-link"
          active-class="active"
        >
          <AppIcon :name="section.icon" />
          <span class="sub-text">
            <span class="sub-label">{{ t(section.label) }}</span>
            <small>{{ t(section.hint) }}</small>
          </span>
        </router-link>
      </nav>

      <div class="content">
        <router-view />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import PageHeader from '@/components/ui/PageHeader.vue'
import AppIcon, { type IconName } from '@/components/ui/AppIcon.vue'
import type { MessageKey } from '@/i18n'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

interface Section {
  name: string
  label: MessageKey
  hint: MessageKey
  icon: IconName
}

const sections: Section[] = [
  { name: 'settings-general', label: 'settings.nav.general', hint: 'settings.nav.generalHint', icon: 'monitor' },
  { name: 'settings-invoices', label: 'settings.nav.invoices', hint: 'settings.nav.invoicesHint', icon: 'invoices' },
  { name: 'settings-country', label: 'settings.nav.country', hint: 'settings.nav.countryHint', icon: 'landmark' },
  { name: 'settings-email', label: 'settings.nav.email', hint: 'settings.nav.emailHint', icon: 'send' },
  { name: 'settings-data', label: 'settings.nav.data', hint: 'settings.nav.dataHint', icon: 'download' }
]
</script>

<style scoped>
.settings-page {
  max-width: 76rem;
}

.layout {
  display: grid;
  grid-template-columns: 15rem minmax(0, 1fr);
  gap: 1.5rem;
  align-items: start;
}

.sub-nav {
  position: sticky;
  top: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.sub-link {
  display: flex;
  align-items: flex-start;
  gap: 0.65rem;
  padding: 0.55rem 0.7rem;
  border-radius: var(--radius-md);
  color: var(--color-text);
  text-decoration: none;
  transition: background 0.15s;
}

.sub-link:hover {
  background: var(--color-surface-muted);
  text-decoration: none;
}

.sub-link .app-icon {
  margin-top: 0.1rem;
  color: var(--color-text-muted);
}

.sub-link.active {
  background: var(--color-primary-soft);
}

.sub-link.active .app-icon,
.sub-link.active .sub-label {
  color: var(--color-primary);
}

.sub-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.sub-label {
  font-size: var(--text-md);
  font-weight: 600;
}

.sub-text small {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-width: 0;
}

/* On a phone the menu becomes a row of chips above the section */
@media (max-width: 760px) {
  .layout {
    grid-template-columns: minmax(0, 1fr);
  }

  .sub-nav {
    position: static;
    flex-direction: row;
    flex-wrap: wrap;
  }

  .sub-link {
    border: 1px solid var(--color-border);
  }

  .sub-text small {
    display: none;
  }
}
</style>

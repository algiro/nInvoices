<template>
  <div class="shell" :class="{ 'nav-open': navOpen }">
    <aside class="sidebar" aria-label="Main navigation">
      <router-link to="/" class="brand" @click="navOpen = false">
        <span class="brand-mark">nI</span>
        <span class="brand-name">nInvoices</span>
      </router-link>

      <nav class="nav">
        <router-link
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          class="nav-link"
          :class="{ active: isActive(item) }"
          @click="navOpen = false"
        >
          <AppIcon :name="item.icon" />
          <span>{{ item.label }}</span>
        </router-link>
      </nav>

      <div class="sidebar-footer">
        <BaseButton variant="primary" icon="plus" to="/invoices/new" class="new-invoice" @click="navOpen = false">
          New invoice
        </BaseButton>
      </div>
    </aside>

    <div class="backdrop" @click="navOpen = false"></div>

    <div class="main">
      <header class="topbar">
        <button type="button" class="menu-btn" aria-label="Open navigation" @click="navOpen = true">
          <AppIcon name="menu" />
        </button>

        <nav class="breadcrumbs" aria-label="Breadcrumb">
          <template v-for="(crumb, index) in breadcrumbs" :key="index">
            <AppIcon v-if="index > 0" name="chevronRight" class="crumb-sep" />
            <router-link v-if="crumb.to && index < breadcrumbs.length - 1" :to="crumb.to" class="crumb">
              {{ crumb.label }}
            </router-link>
            <span v-else class="crumb current" aria-current="page">{{ crumb.label }}</span>
          </template>
        </nav>

        <div class="user" @keydown.esc="userOpen = false">
          <button
            type="button"
            class="user-btn"
            :aria-expanded="userOpen"
            aria-haspopup="menu"
            @click="userOpen = !userOpen"
          >
            <span class="avatar">{{ initials }}</span>
            <span class="user-name">{{ authStore.username }}</span>
            <AppIcon name="chevronDown" class="chev" />
          </button>
          <div v-if="userOpen" class="user-menu" role="menu">
            <div class="user-meta">
              <strong>{{ authStore.username }}</strong>
              <span v-if="authStore.email">{{ authStore.email }}</span>
            </div>
            <div class="menu-section">
              <span class="menu-label">Theme</span>
              <ThemeSwitch compact />
              <div class="swatches" role="radiogroup" :aria-label="`${mode === 'dark' ? 'Dark' : 'Light'} theme`">
                <button
                  v-for="option in modeThemes"
                  :key="option.id"
                  type="button"
                  role="radio"
                  class="swatch"
                  :class="{ selected: theme === option.id }"
                  :aria-checked="theme === option.id"
                  :title="option.name"
                  :aria-label="option.name"
                  @click="setTheme(option.id)"
                >
                  <!-- drawn with the theme's own variables -->
                  <span class="swatch-fill" :data-theme="option.id"><span class="swatch-accent"></span></span>
                </button>
                <router-link to="/settings" class="all-themes" @click="userOpen = false">All themes</router-link>
              </div>
            </div>
            <button type="button" class="menu-item" role="menuitem" :disabled="loggingOut" @click="logout">
              <AppIcon name="logout" />
              {{ loggingOut ? 'Signing out…' : 'Sign out' }}
            </button>
          </div>
        </div>
      </header>

      <main class="content">
        <router-view />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AppIcon, { type IconName } from '@/components/ui/AppIcon.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import ThemeSwitch from '@/components/ui/ThemeSwitch.vue'
import { useToast } from '@/composables/useToast'
import { usePageTitle } from '@/composables/usePageTitle'
import { THEMES, useTheme } from '@/composables/useTheme'

interface NavItem {
  to: string
  label: string
  icon: IconName
  section: string
}

const navItems: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: 'dashboard', section: 'dashboard' },
  { to: '/customers', label: 'Customers', icon: 'customers', section: 'customers' },
  { to: '/invoices', label: 'Invoices', icon: 'invoices', section: 'invoices' },
  { to: '/settings', label: 'Settings', icon: 'settings', section: 'settings' }
]

const route = useRoute()
const authStore = useAuthStore()
const toast = useToast()
const pageTitle = usePageTitle()

const { mode, theme, setTheme } = useTheme()
const modeThemes = computed(() => THEMES.filter(t => t.mode === mode.value))

const navOpen = ref(false)
const userOpen = ref(false)
const loggingOut = ref(false)

function isActive(item: NavItem): boolean {
  return (route.meta.section as string | undefined) === item.section
}

const breadcrumbs = computed(() => {
  const crumbs: { label: string; to?: string }[] = []
  const parent = route.meta.parent as { label: string; to: string } | undefined
  if (parent) crumbs.push(parent)
  crumbs.push({ label: pageTitle.value ?? (route.meta.title as string) ?? '' })
  return crumbs
})

const initials = computed(() => {
  const name = authStore.username || authStore.email || 'U'
  const parts = name.split(/[\s._@-]+/).filter(Boolean)
  return (parts.length >= 2 ? parts[0][0] + parts[1][0] : name.slice(0, 2)).toUpperCase()
})

async function logout() {
  loggingOut.value = true
  try {
    await authStore.logout()
  } catch (error) {
    toast.failure('Sign out failed', error)
  } finally {
    loggingOut.value = false
    userOpen.value = false
  }
}

function closeUserMenu(event: MouseEvent) {
  if (!(event.target as HTMLElement).closest('.user')) userOpen.value = false
}

onMounted(() => document.addEventListener('click', closeUserMenu))
onBeforeUnmount(() => document.removeEventListener('click', closeUserMenu))

watch(() => route.fullPath, () => {
  navOpen.value = false
  userOpen.value = false
})

</script>

<style scoped>
.shell {
  display: grid;
  grid-template-columns: var(--sidebar-width) minmax(0, 1fr);
  min-height: 100vh;
}

/* ---------- sidebar ---------- */
.sidebar {
  position: sticky;
  top: 0;
  height: 100vh;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.9rem 0.75rem;
  background: var(--sidebar-bg);
  border-right: 1px solid var(--sidebar-border);
  backdrop-filter: var(--panel-blur);
  z-index: 50;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  padding: 0.35rem 0.5rem 1.1rem;
  color: var(--sidebar-text-strong);
  text-decoration: none;
}

.brand:hover {
  text-decoration: none;
}

.brand-mark {
  width: 1.9rem;
  height: 1.9rem;
  display: grid;
  place-items: center;
  border-radius: var(--radius-md);
  background: var(--sidebar-cta-bg);
  color: var(--sidebar-cta-text);
  box-shadow: var(--accent-glow);
  font-weight: 700;
  font-size: var(--text-sm);
  letter-spacing: 0.02em;
}

.brand-name {
  font-weight: 700;
  font-size: 1.05rem;
}

.nav {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.nav-link {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  padding: 0.55rem 0.7rem;
  border-radius: var(--radius-md);
  color: var(--sidebar-text);
  font-size: var(--text-md);
  font-weight: 500;
  text-decoration: none;
  transition: background 0.15s, color 0.15s;
}

.nav-link:hover {
  background: var(--sidebar-hover-bg);
  color: var(--sidebar-text-strong);
  text-decoration: none;
}

.nav-link .app-icon {
  color: var(--sidebar-icon);
}

.nav-link.active {
  background: var(--sidebar-active-bg);
  color: var(--sidebar-active-text);
  font-weight: 600;
}

.nav-link.active .app-icon {
  color: var(--sidebar-active-text);
}

.sidebar-footer {
  margin-top: auto;
  padding-top: 0.75rem;
}

.new-invoice {
  width: 100%;
}

/* the sidebar's own call to action, legible on a coloured sidebar */
.new-invoice.variant-primary,
.new-invoice.variant-primary:hover:not(:disabled) {
  background: var(--sidebar-cta-bg);
  border-color: transparent;
  color: var(--sidebar-cta-text);
}

.backdrop {
  display: none;
}

/* ---------- main ---------- */
.main {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.topbar {
  position: sticky;
  top: 0;
  z-index: 40;
  height: var(--topbar-height);
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0 1.5rem;
  background: var(--color-topbar);
  backdrop-filter: blur(6px);
  border-bottom: 1px solid var(--color-border);
}

.menu-btn {
  display: none;
  padding: 0.35rem;
  border: 0;
  background: transparent;
  color: var(--color-text-secondary);
}

.breadcrumbs {
  flex: 1;
  display: flex;
  align-items: center;
  gap: 0.35rem;
  min-width: 0;
  font-size: var(--text-md);
}

.crumb {
  color: var(--color-text-muted);
  white-space: nowrap;
}

.crumb.current {
  color: var(--color-text);
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
}

.crumb-sep {
  width: 0.9rem;
  height: 0.9rem;
  color: var(--color-text-subtle);
}

.user {
  position: relative;
}

.user-btn {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.25rem 0.45rem 0.25rem 0.25rem;
  border: 1px solid transparent;
  border-radius: 999px;
  background: transparent;
  color: var(--color-text-secondary);
}

.user-btn:hover {
  background: var(--color-surface-sunken);
  border-color: transparent;
  color: var(--color-text);
}

.avatar {
  width: 1.8rem;
  height: 1.8rem;
  display: grid;
  place-items: center;
  border-radius: 50%;
  background: var(--color-surface-sunken);
  border: 1px solid var(--color-border);
  font-size: var(--text-xs);
  font-weight: 700;
  color: var(--color-text-secondary);
}

.user-name {
  font-size: var(--text-md);
  font-weight: 500;
}

.chev {
  width: 0.9rem;
  height: 0.9rem;
}

.user-menu {
  position: absolute;
  right: 0;
  top: calc(100% + 0.4rem);
  min-width: 14rem;
  padding: 0.35rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-md);
}

.user-meta {
  display: flex;
  flex-direction: column;
  padding: 0.5rem 0.6rem 0.6rem;
  border-bottom: 1px solid var(--color-border);
  margin-bottom: 0.3rem;
  font-size: var(--text-md);
}

.user-meta span {
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.menu-section {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.4rem 0.45rem 0.6rem;
  margin-bottom: 0.3rem;
  border-bottom: 1px solid var(--color-border);
}

.swatches {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  padding-top: 0.15rem;
}

.swatch {
  width: 1.7rem;
  height: 1.7rem;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: 50%;
  background: transparent;
  cursor: pointer;
}

.swatch.selected {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 2px var(--color-primary);
}

.swatch-fill {
  display: grid;
  place-items: center;
  width: 100%;
  height: 100%;
  border-radius: 50%;
  background: var(--sidebar-bg);
}

.swatch-accent {
  width: 45%;
  height: 45%;
  border-radius: 50%;
  background: var(--accent-gradient);
}

.all-themes {
  margin-left: auto;
  font-size: var(--text-sm);
}

.menu-label {
  font-size: var(--text-xs);
  font-weight: 600;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.menu-item {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.45rem 0.6rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  text-align: left;
}

.menu-item:hover:not(:disabled) {
  background: var(--color-surface-sunken);
  color: var(--color-text);
}

.content {
  flex: 1;
  min-width: 0;
  padding: 1.5rem;
}

/* ---------- narrow screens: sidebar becomes a drawer ---------- */
@media (max-width: 900px) {
  .shell {
    grid-template-columns: minmax(0, 1fr);
  }

  .sidebar {
    position: fixed;
    left: 0;
    top: 0;
    width: 16rem;
    transform: translateX(-100%);
    transition: transform 0.18s ease;
    box-shadow: var(--shadow-lg);
  }

  .nav-open .sidebar {
    transform: translateX(0);
  }

  .nav-open .backdrop {
    display: block;
    position: fixed;
    inset: 0;
    z-index: 45;
    background: var(--color-scrim);
  }

  .menu-btn {
    display: inline-flex;
  }

  .topbar {
    padding: 0 1rem;
  }

  .user-name {
    display: none;
  }

  .content {
    padding: 1rem;
  }
}
</style>

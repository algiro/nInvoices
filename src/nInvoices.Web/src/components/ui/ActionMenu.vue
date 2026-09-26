<template>
  <button
    ref="trigger"
    type="button"
    class="menu-trigger"
    :class="{ open }"
    :aria-label="label"
    :title="label"
    aria-haspopup="menu"
    :aria-expanded="open"
    @click.stop="toggle"
  >
    <AppIcon name="more" />
  </button>

  <Teleport to="body">
    <div
      v-if="open"
      ref="menu"
      class="action-menu"
      role="menu"
      :style="position"
      @keydown="onKeydown"
    >
      <template v-for="(item, index) in items" :key="index">
        <div v-if="item.separator" class="separator" role="separator"></div>
        <button
          v-else
          type="button"
          role="menuitem"
          class="menu-item"
          :class="{ danger: item.danger }"
          :disabled="item.disabled"
          @click.stop="select(item)"
        >
          <AppIcon v-if="item.icon" :name="item.icon" />
          <span>{{ item.label }}</span>
        </button>
      </template>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, nextTick, onBeforeUnmount } from 'vue'
import AppIcon, { type IconName } from './AppIcon.vue'

export interface ActionMenuItem {
  label?: string
  icon?: IconName
  danger?: boolean
  disabled?: boolean
  separator?: boolean
  run?: () => void
}

withDefaults(defineProps<{
  items: ActionMenuItem[]
  label?: string
}>(), {
  label: 'More actions'
})

const open = ref(false)
const trigger = ref<HTMLElement | null>(null)
const menu = ref<HTMLElement | null>(null)
const position = ref<Record<string, string>>({})

// The menu lives in <body> so scrolling tables can't clip it; it is placed against the trigger
async function toggle() {
  if (open.value) return close()
  open.value = true
  await nextTick()
  place()
  menu.value?.querySelector<HTMLElement>('.menu-item:not(:disabled)')?.focus()
  document.addEventListener('click', onOutside, true)
  window.addEventListener('scroll', close, true)
  window.addEventListener('resize', close)
}

function place() {
  const rect = trigger.value?.getBoundingClientRect()
  const menuRect = menu.value?.getBoundingClientRect()
  if (!rect || !menuRect) return
  const below = rect.bottom + 4 + menuRect.height <= window.innerHeight
  const top = below ? rect.bottom + 4 : Math.max(8, rect.top - 4 - menuRect.height)
  const left = Math.max(8, Math.min(rect.right - menuRect.width, window.innerWidth - menuRect.width - 8))
  position.value = { top: `${top}px`, left: `${left}px` }
}

function close() {
  open.value = false
  document.removeEventListener('click', onOutside, true)
  window.removeEventListener('scroll', close, true)
  window.removeEventListener('resize', close)
}

function onOutside(event: MouseEvent) {
  const target = event.target as Node
  if (!menu.value?.contains(target) && !trigger.value?.contains(target)) close()
}

function select(item: ActionMenuItem) {
  close()
  trigger.value?.focus()
  item.run?.()
}

function onKeydown(event: KeyboardEvent) {
  const items = [...(menu.value?.querySelectorAll<HTMLElement>('.menu-item:not(:disabled)') ?? [])]
  const index = items.indexOf(document.activeElement as HTMLElement)
  if (event.key === 'Escape') {
    close()
    trigger.value?.focus()
  } else if (event.key === 'ArrowDown') {
    items[(index + 1) % items.length]?.focus()
  } else if (event.key === 'ArrowUp') {
    items[(index - 1 + items.length) % items.length]?.focus()
  } else if (event.key === 'Tab') {
    close()
    return
  } else {
    return
  }
  event.preventDefault()
}

onBeforeUnmount(close)
</script>

<style scoped>
.menu-trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0.3rem;
  border: 1px solid transparent;
  border-radius: var(--radius-md);
  background: transparent;
  color: var(--color-text-muted);
  cursor: pointer;
}

.menu-trigger:hover,
.menu-trigger.open {
  background: var(--color-surface-sunken);
  border-color: transparent;
  color: var(--color-text);
}

.menu-trigger .app-icon {
  width: 1.1rem;
  height: 1.1rem;
  stroke-width: 3;
}
</style>

<style>
/* teleported to <body>, so not scoped */
.action-menu {
  position: fixed;
  z-index: 1050;
  min-width: 12rem;
  padding: 0.3rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-md);
}

.action-menu .menu-item {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 0.55rem;
  padding: 0.45rem 0.6rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-md);
  text-align: left;
  cursor: pointer;
}

.action-menu .menu-item:hover:not(:disabled),
.action-menu .menu-item:focus-visible {
  background: var(--color-surface-sunken);
  color: var(--color-text);
  outline: none;
}

.action-menu .menu-item.danger {
  color: var(--color-danger);
}

.action-menu .menu-item.danger:hover:not(:disabled),
.action-menu .menu-item.danger:focus-visible {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}

.action-menu .menu-item .app-icon {
  width: 1rem;
  height: 1rem;
}

.action-menu .separator {
  height: 1px;
  margin: 0.3rem 0.2rem;
  background: var(--color-border);
}
</style>

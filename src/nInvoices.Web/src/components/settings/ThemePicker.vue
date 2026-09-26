<template>
  <div class="theme-picker">
    <div class="mode-row">
      <div>
        <strong>Mode</strong>
        <p class="muted">“System” follows your operating system and switches with it.</p>
      </div>
      <ThemeSwitch />
    </div>

    <section v-for="group in groups" :key="group.mode" class="group" :aria-labelledby="`theme-group-${group.mode}`">
      <h3 :id="`theme-group-${group.mode}`">
        {{ group.title }}
        <span v-if="mode === group.mode" class="in-use">shown now</span>
      </h3>
      <div class="options" role="radiogroup" :aria-labelledby="`theme-group-${group.mode}`">
        <button
          v-for="option in group.themes"
          :key="option.id"
          type="button"
          role="radio"
          class="option"
          :class="{ selected: selectedFor(group.mode) === option.id }"
          :aria-checked="selectedFor(group.mode) === option.id"
          @click="setTheme(option.id)"
        >
          <!-- a miniature of the app drawn with the theme's own variables -->
          <span class="preview" :data-theme="option.id" aria-hidden="true">
            <span class="mini-sidebar">
              <span class="mini-brand"></span>
              <span class="mini-nav active"></span>
              <span class="mini-nav"></span>
              <span class="mini-nav"></span>
            </span>
            <span class="mini-main">
              <span class="mini-row">
                <span class="mini-hero"><span class="mini-line light"></span><span class="mini-figure"></span></span>
                <span class="mini-card"><span class="mini-dot" style="--dot: var(--kpi-2)"></span><span class="mini-line"></span></span>
              </span>
              <span class="mini-card chart">
                <span v-for="h in bars" :key="h" class="mini-bar" :style="{ height: `${h}%` }"></span>
              </span>
              <span class="mini-button"></span>
            </span>
          </span>
          <span class="option-text">
            <span class="option-name">
              {{ option.name }}
              <AppIcon v-if="selectedFor(group.mode) === option.id" name="check" class="check" />
            </span>
            <span class="option-description">{{ option.description }}</span>
          </span>
        </button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import AppIcon from '@/components/ui/AppIcon.vue'
import ThemeSwitch from '@/components/ui/ThemeSwitch.vue'
import { THEMES, useTheme, type ThemeMode } from '@/composables/useTheme'

const { mode, lightTheme, darkTheme, setTheme } = useTheme()

const bars = [45, 70, 55, 90, 65, 80]

const groups = computed(() => [
  { mode: 'light' as ThemeMode, title: 'Light theme', themes: THEMES.filter(t => t.mode === 'light') },
  { mode: 'dark' as ThemeMode, title: 'Dark theme', themes: THEMES.filter(t => t.mode === 'dark') }
])

function selectedFor(groupMode: ThemeMode) {
  return groupMode === 'dark' ? darkTheme.value : lightTheme.value
}
</script>

<style scoped>
.theme-picker {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.mode-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem 1.5rem;
}

.mode-row p {
  margin: 0.15rem 0 0;
  font-size: var(--text-sm);
}

.group h3 {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.6rem;
  font-size: var(--text-md);
}

.in-use {
  padding: 0.05rem 0.5rem;
  border-radius: 999px;
  background: var(--color-primary-soft);
  color: var(--color-primary);
  font-size: var(--text-xs);
  font-weight: 600;
}

.options {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(13.5rem, 1fr));
  gap: 0.75rem;
}

.option {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  padding: 0.5rem 0.5rem 0.7rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-text);
  text-align: left;
  cursor: pointer;
  transition: border-color 0.15s, box-shadow 0.15s, transform 0.15s;
}

.option:hover {
  border-color: var(--color-primary-line);
  transform: translateY(-1px);
}

.option.selected {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 2px var(--color-primary);
}

.option-text {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  padding: 0 0.25rem;
}

.option-name {
  display: flex;
  align-items: center;
  gap: 0.35rem;
  font-weight: 600;
  font-size: var(--text-md);
}

.check {
  width: 0.95rem;
  height: 0.95rem;
  color: var(--color-primary);
}

.option-description {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

/* ---------- miniature ---------- */
.preview {
  display: flex;
  height: 7.25rem;
  overflow: hidden;
  border-radius: calc(var(--radius-lg) - 0.2rem);
  border: 1px solid var(--color-border);
  background: var(--app-bg);
}

.mini-sidebar {
  width: 24%;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  padding: 0.4rem 0.3rem;
  background: var(--sidebar-bg);
  border-right: 1px solid var(--sidebar-border);
}

.mini-brand {
  width: 0.8rem;
  height: 0.8rem;
  margin-bottom: 0.2rem;
  border-radius: 0.2rem;
  background: var(--sidebar-cta-bg);
}

.mini-nav {
  height: 0.4rem;
  border-radius: 0.2rem;
  background: var(--sidebar-icon);
  opacity: 0.45;
}

.mini-nav.active {
  height: 0.55rem;
  background: var(--sidebar-active-bg);
  opacity: 1;
}

.mini-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  padding: 0.4rem;
  min-width: 0;
}

.mini-row {
  display: flex;
  gap: 0.3rem;
  height: 2.1rem;
}

.mini-hero,
.mini-card {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 0.3rem;
  border-radius: 0.35rem;
  border: 1px solid var(--panel-border);
  background: var(--panel-bg);
}

.mini-hero {
  flex: 1.3;
  background: var(--hero-bg);
  border-color: var(--hero-border);
}

.mini-card {
  flex: 1;
}

.mini-line {
  width: 60%;
  height: 0.22rem;
  border-radius: 0.2rem;
  background: var(--color-text-muted);
  opacity: 0.6;
}

.mini-line.light {
  background: var(--hero-muted);
}

.mini-figure {
  width: 75%;
  height: 0.4rem;
  border-radius: 0.2rem;
  background: var(--hero-text);
}

.mini-dot {
  width: 0.55rem;
  height: 0.55rem;
  border-radius: 0.15rem;
  background: var(--dot);
}

.mini-card.chart {
  flex: 1;
  flex-direction: row;
  align-items: flex-end;
  gap: 0.2rem;
  padding: 0.3rem 0.35rem;
}

.mini-bar {
  flex: 1;
  border-radius: 0.12rem 0.12rem 0 0;
  background: linear-gradient(180deg, var(--chart-bar), var(--chart-bar-2));
}

.mini-button {
  align-self: flex-end;
  width: 2.2rem;
  height: 0.6rem;
  border-radius: 0.25rem;
  background: var(--accent-gradient);
}

@media (prefers-reduced-motion: reduce) {
  .option:hover {
    transform: none;
  }
}
</style>

<template>
  <div class="preview-pane">
    <div class="preview-toolbar">
      <span class="preview-title">Preview</span>
      <span v-if="loading" class="status muted">Rendering…</span>
      <span v-else-if="stale" class="status warn">Showing the last version that rendered</span>
      <span v-else-if="html" class="status muted">Sample data{{ customerName ? ` for ${customerName}` : '' }}</span>
      <span class="spacer"></span>
      <div class="zoom" role="group" aria-label="Zoom">
        <button type="button" :class="{ active: zoom === 'fit' }" @click="zoom = 'fit'">Fit</button>
        <button type="button" :class="{ active: zoom === 'actual' }" @click="zoom = 'actual'">100%</button>
      </div>
    </div>

    <div ref="viewport" class="viewport">
      <div v-if="!html" class="empty">
        <strong>{{ loading ? 'Rendering preview…' : 'Nothing to preview yet' }}</strong>
        <span v-if="!loading">Write some HTML, or load the sample, and the rendered page appears here.</span>
      </div>
      <div
        v-else
        class="page-frame"
        :style="{ width: `${PAGE_WIDTH * scale}px`, height: `${pageHeight * scale}px` }"
      >
        <!-- sandbox without allow-scripts: template HTML is rendered, never executed -->
        <iframe
          ref="frame"
          class="page"
          title="Template preview"
          sandbox="allow-same-origin"
          :srcdoc="html"
          :style="{ width: `${PAGE_WIDTH}px`, height: `${pageHeight}px`, transform: `scale(${scale})` }"
          @load="measure"
        ></iframe>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'

defineProps<{
  html: string | null
  loading: boolean
  /** The shown HTML is from an earlier version because the current one doesn't render. */
  stale: boolean
  customerName?: string
}>()

// A4 at 96 dpi, the width Chrome uses when the PDF is produced
const PAGE_WIDTH = 794
const A4_HEIGHT = 1123

const viewport = ref<HTMLElement | null>(null)
const frame = ref<HTMLIFrameElement | null>(null)
const zoom = ref<'fit' | 'actual'>('fit')
const available = ref(PAGE_WIDTH)
const pageHeight = ref(A4_HEIGHT)

const scale = computed(() => (zoom.value === 'fit' ? Math.min(1, (available.value - 32) / PAGE_WIDTH) : 1))

// Grow the frame to the rendered document so long reports scroll in the pane, not inside the iframe
function measure() {
  const doc = frame.value?.contentDocument
  if (!doc) return
  pageHeight.value = Math.max(A4_HEIGHT, doc.documentElement.scrollHeight)
}

let observer: ResizeObserver | null = null
onMounted(() => {
  observer = new ResizeObserver(entries => {
    available.value = entries[0].contentRect.width
  })
  if (viewport.value) observer.observe(viewport.value)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<style scoped>
.preview-pane {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  background: var(--color-surface);
}

.preview-toolbar {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  min-height: 2.5rem;
  padding: 0.35rem 0.75rem;
  border-bottom: 1px solid var(--color-border);
  font-size: var(--text-sm);
}

.preview-title {
  font-weight: 600;
  color: var(--color-text);
}

.status.muted {
  color: var(--color-text-muted);
}

.status.warn {
  color: var(--color-warning);
}

.spacer {
  flex: 1;
}

.zoom {
  display: inline-flex;
  gap: 2px;
  padding: 2px;
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-md);
}

.zoom button {
  padding: 0.15rem 0.55rem;
  border: 0;
  border-radius: var(--radius-sm);
  background: transparent;
  color: var(--color-text-secondary);
  font-size: var(--text-xs);
}

.zoom button.active {
  background: var(--color-primary);
  color: var(--color-on-primary);
}

.viewport {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 1rem;
  background: var(--color-surface-sunken);
}

.page-frame {
  margin: 0 auto;
  box-shadow: var(--shadow-md);
  background: #ffffff;
  overflow: hidden;
}

.page {
  display: block;
  border: 0;
  background: #ffffff;
  transform-origin: top left;
}

.empty {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  max-width: 22rem;
  margin: 3rem auto;
  text-align: center;
  font-size: var(--text-md);
  color: var(--color-text-muted);
}

.empty strong {
  color: var(--color-text);
}
</style>

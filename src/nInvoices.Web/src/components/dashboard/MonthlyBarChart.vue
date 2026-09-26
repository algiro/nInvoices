<template>
  <figure class="bar-chart">
    <div ref="plot" class="plot" @mouseleave="hover = null">
      <svg :viewBox="`0 0 ${width} ${height}`" :width="width" :height="height" role="img" :aria-label="label">
        <!-- the theme's two chart colours, from the top of each bar to its base -->
        <defs>
          <linearGradient :id="gradientId" x1="0" x2="0" y1="0" y2="1">
            <stop offset="0" class="stop-top" />
            <stop offset="1" class="stop-base" />
          </linearGradient>
        </defs>

        <!-- recessive grid and y labels -->
        <g v-for="tick in ticks" :key="tick">
          <line :x1="padLeft" :x2="width - padRight" :y1="y(tick)" :y2="y(tick)" class="grid" />
          <text :x="padLeft - 8" :y="y(tick)" class="axis y-label" text-anchor="end" dominant-baseline="middle">
            {{ compact(tick) }}
          </text>
        </g>

        <g v-for="(point, index) in points" :key="point.key">
          <!-- full-band hit target, larger than the mark -->
          <rect
            :x="bandX(index)"
            :y="padTop"
            :width="band"
            :height="plotHeight"
            class="hit"
            tabindex="0"
            :aria-label="`${point.label}: ${format(point.value)}`"
            @mouseenter="hover = index"
            @focus="hover = index"
            @blur="hover = null"
          />
          <path v-if="point.value > 0" :d="columnPath(index, point.value)" class="bar" :fill="`url(#${gradientId})`" :class="{ dim: hover !== null && hover !== index }" />
          <text :x="bandX(index) + band / 2" :y="height - padBottom + 16" class="axis x-label" text-anchor="middle">
            {{ point.short }}
          </text>
        </g>

        <!-- selective direct label: the latest month only -->
        <text
          v-if="latest && latest.value > 0 && hover === null"
          :x="bandX(points.length - 1) + band / 2"
          :y="y(latest.value) - 6"
          class="value-label"
          text-anchor="middle"
        >
          {{ compact(latest.value) }}
        </text>
      </svg>

      <div
        v-if="hover !== null"
        class="tooltip"
        :style="{ left: `${bandX(hover) + band / 2}px`, top: `${Math.min(y(points[hover].value), height - padBottom) - 8}px` }"
        role="status"
      >
        <strong>{{ points[hover].label }}</strong>
        <span>{{ format(points[hover].value) }}</span>
        <small v-if="points[hover].count">{{ points[hover].count }} {{ points[hover].count === 1 ? 'invoice' : 'invoices' }}</small>
      </div>
    </div>

    <!-- the same numbers for screen readers -->
    <table class="sr-only">
      <caption>{{ label }}</caption>
      <thead><tr><th scope="col">Month</th><th scope="col">Invoiced</th></tr></thead>
      <tbody>
        <tr v-for="point in points" :key="point.key"><td>{{ point.label }}</td><td>{{ format(point.value) }}</td></tr>
      </tbody>
    </table>
  </figure>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, useId } from 'vue'

export interface MonthPoint {
  key: string
  label: string // "September 2026"
  short: string // "Sep"
  value: number
  count?: number
}

const props = defineProps<{
  points: MonthPoint[]
  format: (value: number) => string
  label: string
}>()

const plot = ref<HTMLElement | null>(null)
const gradientId = `bar-gradient-${useId()}`
const width = ref(640)
const height = 220
const padTop = 18
const padBottom = 26
const padLeft = 52
const padRight = 8
const hover = ref<number | null>(null)

const plotHeight = height - padTop - padBottom
const band = computed(() => (width.value - padLeft - padRight) / Math.max(1, props.points.length))
// columns are capped at 24px; the rest of the band stays air
const barWidth = computed(() => Math.min(24, Math.max(6, band.value - 8)))
const latest = computed(() => props.points[props.points.length - 1])

// A "nice" top value so ticks land on round numbers
const niceMax = computed(() => {
  const max = Math.max(0, ...props.points.map(p => p.value))
  if (max === 0) return 1
  const magnitude = 10 ** Math.floor(Math.log10(max))
  const step = [1, 2, 2.5, 5, 10].find(s => s * magnitude * 4 >= max) ?? 10
  return step * magnitude * 4
})

const ticks = computed(() => [0, 1, 2, 3, 4].map(i => (niceMax.value / 4) * i))

function y(value: number): number {
  return padTop + plotHeight - (value / niceMax.value) * plotHeight
}

function bandX(index: number): number {
  return padLeft + index * band.value
}

// 4px rounded data end, square at the baseline
function columnPath(index: number, value: number): string {
  const x = bandX(index) + (band.value - barWidth.value) / 2
  const w = barWidth.value
  const top = y(value)
  const base = y(0)
  const r = Math.min(4, w / 2, base - top)
  return `M${x},${base} V${top + r} Q${x},${top} ${x + r},${top} H${x + w - r} Q${x + w},${top} ${x + w},${top + r} V${base} Z`
}

function compact(value: number): string {
  return new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 }).format(value)
}

let observer: ResizeObserver | null = null
onMounted(() => {
  observer = new ResizeObserver(entries => {
    width.value = Math.max(280, Math.floor(entries[0].contentRect.width))
  })
  if (plot.value) observer.observe(plot.value)
})
onBeforeUnmount(() => observer?.disconnect())
</script>

<style scoped>
.bar-chart {
  margin: 0;
}

.plot {
  position: relative;
  width: 100%;
}

svg {
  display: block;
  max-width: 100%;
  overflow: visible;
}

.grid {
  stroke: var(--color-border);
  stroke-width: 1;
}

.axis {
  fill: var(--color-text-muted);
  font-size: 11px;
  font-variant-numeric: tabular-nums;
}

.bar {
  transition: opacity 0.12s;
}

.stop-top {
  stop-color: var(--chart-bar);
}

.stop-base {
  stop-color: var(--chart-bar-2);
}

.bar.dim {
  opacity: 0.45;
}

.hit {
  fill: transparent;
  cursor: default;
  outline: none;
}

.hit:focus-visible {
  fill: var(--color-primary-soft);
}

.value-label {
  fill: var(--color-text);
  font-size: 11px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.tooltip {
  position: absolute;
  transform: translate(-50%, -100%);
  display: flex;
  flex-direction: column;
  gap: 0.05rem;
  padding: 0.45rem 0.6rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  box-shadow: var(--shadow-md);
  font-size: var(--text-sm);
  white-space: nowrap;
  pointer-events: none;
}

.tooltip strong {
  color: var(--color-text);
}

.tooltip span {
  color: var(--color-text);
  font-variant-numeric: tabular-nums;
}

.tooltip small {
  color: var(--color-text-muted);
}
</style>

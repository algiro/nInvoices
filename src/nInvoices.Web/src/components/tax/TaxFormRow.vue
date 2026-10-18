<template>
  <article class="tax-form">
    <div class="summary">
      <div class="title">
        <h3>{{ form.name }}</h3>
        <span class="deadline muted">{{ t('tax.row.by', { date: formatDate(form.deadline) }) }}</span>
      </div>

      <StatusPill :tone="status.tone">{{ status.label }}</StatusPill>

      <dl class="amounts">
        <div>
          <dt>{{ t('tax.row.estimated') }}</dt>
          <dd class="num">{{ formatMoney(form.estimated, currency) }}</dd>
        </div>
        <div v-if="form.payment">
          <dt>{{ t('tax.row.paidOn', { date: formatDate(form.payment.paidOn, { day: 'numeric', month: 'short' }) }) }}</dt>
          <dd class="num">
            {{ formatMoney(form.payment.amount, form.payment.currency) }}
            <small v-if="difference !== 0" class="difference" :class="difference > 0 ? 'over' : 'under'">
              {{ difference > 0 ? '+' : '−' }}{{ formatMoney(Math.abs(difference), currency) }}
            </small>
          </dd>
        </div>
      </dl>

      <div class="buttons">
        <BaseButton size="sm" variant="ghost" :icon="expanded ? 'chevronDown' : 'chevronRight'" :aria-expanded="expanded" @click="expanded = !expanded">
          {{ t('tax.row.how') }}
        </BaseButton>
        <BaseButton size="sm" :icon="form.payment ? 'edit' : 'paid'" @click="emit('record', form)">
          {{ form.payment ? t('tax.row.payment') : t('tax.row.recordPayment') }}
        </BaseButton>
      </div>
    </div>

    <table v-if="expanded" class="lines">
      <caption class="sr-only">{{ t('tax.row.caption', { form: form.name }) }}</caption>
      <tbody>
        <tr v-for="(line, index) in form.lines" :key="index" :class="{ detail: !line.box }">
          <th scope="row">
            <span v-if="line.box" class="box">{{ line.box }}</span>
            {{ line.label }}
          </th>
          <td class="num">{{ formatMoney(line.amount, currency) }}</td>
        </tr>
      </tbody>
    </table>
  </article>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import type { TaxFormDto, TaxPeriodDto } from '@/types'
import BaseButton from '@/components/ui/BaseButton.vue'
import StatusPill from '@/components/ui/StatusPill.vue'
import { formatDate, formatMoney } from '@/utils/format'
import { formStatus } from '@/utils/taxPrediction'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const props = defineProps<{
  form: TaxFormDto
  period: TaxPeriodDto
  currency: string
  /** ISO date, for the status */
  today: string
}>()

const emit = defineEmits<{ record: [form: TaxFormDto] }>()

const expanded = ref(false)

const status = computed(() => formStatus(props.form, props.period, props.today))

/** Paid minus estimated, to the cent */
const difference = computed(() =>
  props.form.payment ? Math.round((props.form.payment.amount - props.form.estimated) * 100) / 100 : 0)
</script>

<style scoped>
.tax-form {
  /* The row lays itself out by its own width: next to the sidebar it is much narrower than the window */
  container-type: inline-size;
  padding: 0.9rem 1rem;
  border-top: 1px solid var(--color-border);
}

.tax-form:first-child {
  border-top: none;
}

.summary {
  display: grid;
  grid-template-columns: minmax(12rem, 1fr) auto minmax(14rem, auto) auto;
  align-items: center;
  gap: 0.5rem 1rem;
}

.title h3 {
  font-size: var(--text-md);
  font-weight: 600;
}

.deadline {
  font-size: var(--text-sm);
}

.amounts {
  display: flex;
  gap: 1.5rem;
  margin: 0;
}

.amounts dt {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.amounts dd {
  margin: 0;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.difference {
  margin-left: 0.3rem;
  font-weight: 500;
}

.difference.over {
  color: var(--color-warning, var(--color-text-muted));
}

.difference.under {
  color: var(--color-success, var(--color-text-muted));
}

.buttons {
  display: flex;
  justify-content: flex-end;
  gap: 0.4rem;
}

.lines {
  width: 100%;
  margin-top: 0.75rem;
  border-collapse: collapse;
  font-size: var(--text-md);
}

.lines th,
.lines td {
  padding: 0.35rem 0.5rem;
  border-top: 1px solid var(--color-border);
  text-align: left;
  font-weight: 400;
}

.lines td.num {
  text-align: right;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.lines tr.detail th,
.lines tr.detail td {
  padding-left: 2.6rem;
  color: var(--color-text-muted);
  font-size: var(--text-sm);
}

.box {
  display: inline-block;
  min-width: 1.9rem;
  margin-right: 0.4rem;
  padding: 0 0.3rem;
  border-radius: var(--radius-sm);
  background: var(--color-surface-sunken);
  font-family: var(--font-mono, monospace);
  font-size: var(--text-sm);
  text-align: center;
}

@container (max-width: 52rem) {
  .summary {
    grid-template-columns: 1fr auto;
  }

  .amounts,
  .buttons {
    grid-column: 1 / -1;
  }

  .buttons {
    justify-content: flex-start;
  }
}
</style>

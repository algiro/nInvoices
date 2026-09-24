<template>
  <form class="project-form" novalidate @submit.prevent="handleSubmit">
    <BaseField label="Name" for="project-name" required :error="errors.name">
      <input
        id="project-name"
        v-model.trim="form.name"
        type="text"
        maxlength="200"
        class="control"
        placeholder="e.g. Website redesign"
      />
    </BaseField>

    <label v-if="projectId" class="toggle" for="project-active">
      <input id="project-active" v-model="form.isActive" type="checkbox" />
      <span>
        <strong>Active</strong>
        <small>Inactive projects aren't suggested when entering days, but stay on past invoices and reports.</small>
      </span>
    </label>

    <div class="form-actions">
      <BaseButton :disabled="saving" @click="emit('cancel')">Cancel</BaseButton>
      <BaseButton type="submit" variant="primary" :loading="saving">
        {{ projectId ? 'Save changes' : 'Add project' }}
      </BaseButton>
    </div>
  </form>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useProjectsStore } from '@/stores/projects'
import type { CreateProjectDto } from '@/types'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import { errorMessage } from '@/composables/useToast'

interface Props {
  customerId: number
  projectId?: number
}

interface Emits {
  (e: 'success'): void
  (e: 'cancel'): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()
const projectsStore = useProjectsStore()

const saving = ref(false)
const errors = reactive<Record<string, string>>({})
const form = reactive({
  name: '',
  isActive: true
})

onMounted(() => {
  if (props.projectId) {
    const existing = projectsStore.projects.find(p => p.id === props.projectId)
    if (existing) {
      form.name = existing.name
      form.isActive = existing.isActive
    }
  }
})

async function handleSubmit() {
  delete errors.name
  if (!form.name.trim()) {
    errors.name = 'Enter a project name.'
    document.getElementById('project-name')?.focus()
    return
  }

  try {
    saving.value = true
    if (props.projectId) {
      await projectsStore.update(props.projectId, { name: form.name.trim(), isActive: form.isActive })
    } else {
      await projectsStore.create({ customerId: props.customerId, name: form.name.trim() } as CreateProjectDto)
    }
    emit('success')
  } catch (error) {
    errors.name = `The project could not be saved: ${errorMessage(error)}`
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.project-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.toggle {
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  padding: 0.7rem 0.8rem;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  cursor: pointer;
}

.toggle input {
  width: 1rem;
  height: 1rem;
  margin-top: 0.15rem;
  accent-color: var(--color-primary);
}

.toggle span {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
  font-size: var(--text-md);
}

.toggle small {
  font-size: var(--text-sm);
  color: var(--color-text-muted);
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding-top: 0.25rem;
}
</style>

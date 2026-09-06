<template>
  <div class="project-form">
    <form @submit.prevent="handleSubmit" class="form-content">
      <div class="form-group">
        <label for="name" class="form-label">
          Project Name <span class="text-red-500">*</span>
        </label>
        <input
          id="name"
          v-model.trim="form.name"
          type="text"
          required
          maxlength="200"
          class="form-control"
          :class="{ 'border-red-500': errors.name }"
          placeholder="e.g. Website redesign"
        />
        <p v-if="errors.name" class="text-red-500 text-sm mt-1">{{ errors.name }}</p>
      </div>

      <div v-if="projectId" class="form-group">
        <label class="form-label checkbox-label">
          <input v-model="form.isActive" type="checkbox" />
          Active
        </label>
        <p class="text-xs text-gray-500 mt-1">
          Inactive projects are hidden from the calendar suggestions but kept on past reports.
        </p>
      </div>

      <div class="form-actions">
        <button type="button" @click="handleCancel" class="btn-secondary" :disabled="loading">
          Cancel
        </button>
        <button type="submit" class="btn-primary" :disabled="loading">
          {{ loading ? 'Saving...' : 'Save Project' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useProjectsStore } from '@/stores/projects'
import type { CreateProjectDto } from '@/types'

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

const loading = ref(false)
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

function validate(): boolean {
  Object.keys(errors).forEach(key => delete errors[key])
  if (!form.name || form.name.trim().length === 0) {
    errors.name = 'Project name is required'
    return false
  }
  return true
}

async function handleSubmit() {
  if (!validate()) return

  try {
    loading.value = true
    if (props.projectId) {
      await projectsStore.update(props.projectId, {
        name: form.name.trim(),
        isActive: form.isActive
      })
    } else {
      await projectsStore.create({
        customerId: props.customerId,
        name: form.name.trim()
      } as CreateProjectDto)
    }
    emit('success')
  } catch (error: any) {
    errors.name = error.message || 'Failed to save project'
  } finally {
    loading.value = false
  }
}

function handleCancel() {
  emit('cancel')
}
</script>

<style scoped>
.project-form {
  background: white;
  padding: 1.5rem;
  border-radius: 0.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.form-content {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.form-group {
  display: flex;
  flex-direction: column;
}

.form-label {
  font-size: 0.875rem;
  font-weight: 500;
  color: #374151;
  margin-bottom: 0.5rem;
}

.checkbox-label {
  flex-direction: row;
  align-items: center;
  gap: 0.5rem;
}

.form-control {
  width: 100%;
  padding: 0.75rem 1rem;
  border: 1px solid #d1d5db;
  border-radius: 0.375rem;
  font-size: 1rem;
  transition: all 0.2s;
}

.form-control:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.1);
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 1rem;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
}

.btn-primary,
.btn-secondary {
  padding: 0.75rem 1.5rem;
  border-radius: 0.375rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  border: none;
}

.btn-primary {
  background: #2563eb;
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: #1d4ed8;
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: white;
  color: #374151;
  border: 1px solid #d1d5db;
}

.btn-secondary:hover:not(:disabled) {
  background: #f9fafb;
}
</style>

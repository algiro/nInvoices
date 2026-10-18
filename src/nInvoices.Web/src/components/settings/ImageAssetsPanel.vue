<template>
  <BasePanel :title="t('images.title')" :description="t('images.description')">
    <form class="upload" novalidate @submit.prevent="handleUploadImage">
      <BaseField :label="t('images.alias')" for="imageAlias" :help="t('images.aliasHelp')">
        <input id="imageAlias" v-model="imageAlias" type="text" class="control" placeholder="companyLogo" />
      </BaseField>
      <BaseField :label="t('images.image')" for="imageFile" :help="t('images.imageHelp')" :error="imageUploadError">
        <input
          id="imageFile"
          ref="imageFileInput"
          type="file"
          accept="image/png,image/jpeg,image/gif,image/svg+xml,image/webp"
          class="control file"
          @change="handleImageFileSelected"
        />
      </BaseField>
      <BaseButton type="submit" variant="primary" icon="plus" :loading="imageUploading" :disabled="!canUpload" class="upload-btn">{{ t('images.upload') }}</BaseButton>
    </form>

    <LoadingState v-if="imageAssetsLoading" :label="t('images.loading')" />
    <EmptyState
      v-else-if="imageAssets.length === 0"
      icon="template"
      :title="t('images.emptyTitle')"
      :description="t('images.emptyDescription')"
      compact
    />
    <ul v-else class="images">
      <li v-for="asset in imageAssets" :key="asset.id" class="image-card">
        <div class="thumb">
          <img
            v-if="imageDataCache[asset.id]"
            :src="`data:${asset.contentType};base64,${imageDataCache[asset.id]}`"
            :alt="asset.alias"
          />
          <button v-else type="button" class="load-thumb" @click="loadImageData(asset.id)">{{ t('images.showPreview') }}</button>
        </div>
        <div class="image-body">
          <strong>{{ asset.alias }}</strong>
          <span class="muted">{{ asset.fileName }} · {{ formatFileSize(asset.fileSize) }}</span>
          <div class="snippet">
            <code>[[ Image "{{ asset.alias }}" ]]</code>
            <BaseButton size="sm" variant="ghost" @click="copySnippet(asset.alias)">{{ t('images.copy') }}</BaseButton>
          </div>
        </div>
        <BaseButton
          size="sm"
          variant="ghost-danger"
          icon="trash"
          icon-only
          class="image-delete"
          :aria-label="t('common.deleteNamed', { name: asset.alias })"
          :title="t('common.delete')"
          @click="handleDeleteImage(asset)"
        />
      </li>
    </ul>
  </BasePanel>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive } from 'vue'
import { imageAssetsApi } from '@/api'
import type { ImageAssetDto } from '@/api/imageAssets'
import { useToast, errorMessage } from '@/composables/useToast'
import { useConfirm } from '@/composables/useConfirm'
import BasePanel from '@/components/ui/BasePanel.vue'
import BaseField from '@/components/ui/BaseField.vue'
import BaseButton from '@/components/ui/BaseButton.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import LoadingState from '@/components/ui/LoadingState.vue'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()

const toast = useToast()
const { confirm } = useConfirm()

onMounted(loadImageAssets)

const imageAssets = ref<ImageAssetDto[]>([])
const imageAssetsLoading = ref(false)
const imageAlias = ref('')
const imageFile = ref<File | null>(null)
const imageFileInput = ref<HTMLInputElement | null>(null)
const imageUploading = ref(false)
const imageUploadError = ref<string | null>(null)
const imageDataCache = reactive<Record<number, string>>({})

const canUpload = computed(() => imageAlias.value.trim() && imageFile.value)

function handleImageFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  imageFile.value = input.files?.[0] ?? null
  imageUploadError.value = null
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1048576) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / 1048576).toFixed(1)} MB`
}

async function loadImageAssets() {
  imageAssetsLoading.value = true
  try {
    imageAssets.value = await imageAssetsApi.getAll()
    // Auto-load previews for all images
    for (const asset of imageAssets.value) {
      loadImageData(asset.id)
    }
  } catch (error) {
    console.error('Failed to load image assets:', error)
  } finally {
    imageAssetsLoading.value = false
  }
}

async function loadImageData(id: number) {
  if (imageDataCache[id]) return
  try {
    const data = await imageAssetsApi.getById(id)
    imageDataCache[id] = data.base64Data
  } catch (error) {
    console.error('Failed to load image data:', error)
  }
}

async function handleUploadImage() {
  if (!imageAlias.value.trim() || !imageFile.value) return
  imageUploading.value = true
  imageUploadError.value = null
  try {
    await imageAssetsApi.upload(imageAlias.value.trim(), imageFile.value)
    imageAlias.value = ''
    imageFile.value = null
    if (imageFileInput.value) imageFileInput.value.value = ''
    await loadImageAssets()
  } catch (error) {
    imageUploadError.value = errorMessage(error, t('images.uploadFailed'))
  } finally {
    imageUploading.value = false
  }
}

async function copySnippet(alias: string) {
  const snippet = `[[ Image "${alias}" ]]`
  try {
    await navigator.clipboard.writeText(snippet)
    toast.success(t('images.copied'), { message: snippet })
  } catch {
    toast.info(t('images.copyThis'), { message: snippet })
  }
}

async function handleDeleteImage(asset: ImageAssetDto) {
  if (!(await confirm({ title: t('images.deleteTitle'), message: t('images.deleteMessage', { name: asset.alias }), confirmLabel: t('common.delete'), tone: 'danger' }))) return
  try {
    await imageAssetsApi.delete(asset.id)
    delete imageDataCache[asset.id]
    await loadImageAssets()
  } catch (error) {
    toast.failure(t('images.deleteFailed'), error)
  }
}
</script>

<style scoped>
code {
  padding: 0.05rem 0.3rem;
  border-radius: var(--radius-sm);
  background: var(--color-surface-sunken);
  font-size: 0.85em;
}

/* images */
.upload {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr) auto;
  gap: 0.75rem;
  align-items: start;
  margin-bottom: 1.25rem;
}

.upload-btn {
  margin-top: 1.55rem;
}

@media (max-width: 760px) {
  .upload {
    grid-template-columns: minmax(0, 1fr);
  }

  .upload-btn {
    margin-top: 0;
    justify-self: start;
  }
}

.control.file {
  padding: 0.35rem 0.5rem;
}

.images {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(17rem, 1fr));
  gap: 0.75rem;
  margin: 0;
  padding: 0;
  list-style: none;
}

.image-card {
  position: relative;
  display: flex;
  flex-direction: column;
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  overflow: hidden;
  background: var(--color-surface);
}

.thumb {
  height: 7.5rem;
  display: grid;
  place-items: center;
  padding: 0.75rem;
  background:
    repeating-conic-gradient(var(--color-surface-sunken) 0% 25%, var(--color-surface) 0% 50%) 0 0 / 16px 16px;
  border-bottom: 1px solid var(--color-border);
}

.thumb img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}

.load-thumb {
  border: 0;
  background: transparent;
  color: var(--color-primary);
  font-size: var(--text-sm);
}

.image-body {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  padding: 0.65rem 0.75rem 0.7rem;
  font-size: var(--text-md);
}

.image-body strong {
  color: var(--color-text);
}

.image-body .muted {
  font-size: var(--text-sm);
}

.snippet {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin-top: 0.4rem;
}

.snippet code {
  overflow-wrap: anywhere;
}

.image-delete {
  position: absolute;
  top: 0.4rem;
  right: 0.4rem;
  background: var(--color-surface);
}
</style>

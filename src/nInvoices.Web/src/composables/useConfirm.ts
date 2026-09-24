import { reactive } from 'vue'

export interface ConfirmOptions {
  title: string
  message?: string
  confirmLabel?: string
  cancelLabel?: string
  /** 'danger' styles the confirm button for destructive actions. */
  tone?: 'default' | 'danger'
}

interface ConfirmState extends ConfirmOptions {
  open: boolean
  resolve: ((value: boolean) => void) | null
}

// Single dialog instance shared by the whole app, rendered by ConfirmHost in App.vue
const state = reactive<ConfirmState>({
  open: false,
  title: '',
  resolve: null
})

function confirm(options: ConfirmOptions): Promise<boolean> {
  // A second request while one is open cancels the first
  state.resolve?.(false)
  Object.assign(state, {
    message: undefined,
    confirmLabel: undefined,
    cancelLabel: undefined,
    tone: 'default',
    ...options,
    open: true
  })
  return new Promise<boolean>(resolve => {
    state.resolve = resolve
  })
}

function settle(value: boolean) {
  state.resolve?.(value)
  state.resolve = null
  state.open = false
}

export function useConfirm() {
  return { state, confirm, settle }
}

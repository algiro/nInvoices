import { ref } from 'vue'

/**
 * Lets a view replace its route's static title (e.g. "Customer details") with the record's own
 * name ("Nea srl") in the breadcrumb and browser tab. The router clears it on every navigation.
 */
const pageTitle = ref<string | null>(null)

export function setPageTitle(title: string | null | undefined) {
  pageTitle.value = title || null
  if (title) document.title = `${title} · nInvoices`
}

export function clearPageTitle() {
  pageTitle.value = null
}

export function usePageTitle() {
  return pageTitle
}

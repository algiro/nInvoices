import { computed, ref } from 'vue'
import { taxPredictionApi } from '@/api'
import { t } from '@/i18n'

/**
 * What the user's VAT is called ("IVA / IGIC" in Spain), from the country they pay taxes in (Settings → Taxes);
 * "VAT" until that is known or when it isn't set. Loaded once per use.
 */
export function useIndirectTaxName() {
  const name = ref<string | null>(null)

  taxPredictionApi.getSettings()
    .then(settings => {
      const country = settings.countries.find(c => c.countryCode === settings.profile?.countryCode)
      if (country) name.value = country.indirectTaxName
    })
    .catch(() => {
      // Only a label: the generic name will do
    })

  return computed(() => name.value ?? t('common.vat'))
}

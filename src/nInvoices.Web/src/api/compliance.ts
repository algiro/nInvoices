import { apiClient } from './client';
import type { ComplianceCountryDto, UpdateComplianceSettingsDto } from '@/types';

export const complianceApi = {
  /** The country regimes the installation offers (empty if none), with the user's settings */
  getCountries: async (): Promise<ComplianceCountryDto[]> => {
    return apiClient.get<ComplianceCountryDto[]>('/api/compliance/countries');
  },

  /** Turning a country on checks the settings against its rules (400 with `issues` otherwise) */
  updateCountry: async (countryCode: string, dto: UpdateComplianceSettingsDto): Promise<ComplianceCountryDto> => {
    return apiClient.put<ComplianceCountryDto>(`/api/compliance/countries/${countryCode}`, dto);
  }
};

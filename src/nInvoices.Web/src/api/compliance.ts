import { apiClient } from './client';
import type { ComplianceCountryDto, UpdateComplianceSettingsDto } from '@/types';

export const complianceApi = {
  /** Stores the signing certificate (a .p12/.pfx file, base64) of a country; it is checked and kept encrypted */
  setCertificate: async (countryCode: string, pfxBase64: string, password: string): Promise<ComplianceCountryDto> => {
    return apiClient.put<ComplianceCountryDto>(`/api/compliance/countries/${countryCode}/certificate`, { pfx: pfxBase64, password });
  },

  removeCertificate: async (countryCode: string): Promise<ComplianceCountryDto> => {
    return apiClient.delete<ComplianceCountryDto>(`/api/compliance/countries/${countryCode}/certificate`);
  },

  /** The country regimes the installation offers (empty if none), with the user's settings */
  getCountries: async (): Promise<ComplianceCountryDto[]> => {
    return apiClient.get<ComplianceCountryDto[]>('/api/compliance/countries');
  },

  /** Turning a country on checks the settings against its rules (400 with `issues` otherwise) */
  updateCountry: async (countryCode: string, dto: UpdateComplianceSettingsDto): Promise<ComplianceCountryDto> => {
    return apiClient.put<ComplianceCountryDto>(`/api/compliance/countries/${countryCode}`, dto);
  }
};

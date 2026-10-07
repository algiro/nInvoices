import { describe, it, expect } from 'vitest'
import { apiErrorCode, apiStatus, errorMessage } from './useToast'

const httpError = (status: number, data: unknown) => Object.assign(new Error(`Request failed with status code ${status}`), { response: { status, data } })

describe('error helpers', () => {
  it("prefer the API's own explanation to the HTTP library's message", () => {
    const error = httpError(409, { error: 'Connect Gmail first', code: 'gmail_not_connected' })

    expect(errorMessage(error)).toBe('Connect Gmail first')
    expect(apiErrorCode(error)).toBe('gmail_not_connected')
    expect(apiStatus(error)).toBe(409)
  })

  it('fall back to the error message, then to the caller text', () => {
    expect(errorMessage(httpError(500, 'oops'))).toBe('Request failed with status code 500')
    expect(errorMessage(undefined, 'Failed to load')).toBe('Failed to load')
    expect(errorMessage('')).toBe('Something went wrong. Please try again.')
  })

  it('know nothing of values that are not HTTP errors', () => {
    expect(apiStatus(new Error('x'))).toBeNull()
    expect(apiStatus(null)).toBeNull()
    expect(apiErrorCode({ response: { data: { code: 42 } } })).toBeNull()
  })
})

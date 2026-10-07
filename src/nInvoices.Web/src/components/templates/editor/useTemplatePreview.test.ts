import { describe, it, expect } from 'vitest'
import { parseTemplateError } from './useTemplatePreview'

describe('parseTemplateError', () => {
  it('places a parser error at its line, as a syntax error that blocks saving', () => {
    expect(parseTemplateError('Line 3, Column 12: Unexpected token')).toEqual({ line: 3, column: 12, message: 'Unexpected token', syntax: true })
  })

  it('places a runtime error at its line, without blocking saving', () => {
    expect(parseTemplateError('<input>(7,2) : error : Object `foo` is null')).toEqual({ line: 7, column: 2, message: 'Object `foo` is null', syntax: false })
  })

  it('keeps an email body error in the editor, and a subject error out of it', () => {
    expect(parseTemplateError('Body: Line 2, Column 1: Bad')).toEqual({ line: 2, column: 1, message: 'Bad', syntax: true })
    expect(parseTemplateError('Subject: Line 1, Column 5: Bad')).toEqual({ line: 0, column: 0, message: 'Subject: Bad', syntax: true })
  })

  it('keeps anything else as a message without a position', () => {
    expect(parseTemplateError('Template is empty')).toEqual({ line: 0, column: 0, message: 'Template is empty', syntax: false })
  })
})

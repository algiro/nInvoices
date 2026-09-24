<template>
  <div ref="host" class="code-editor"></div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount } from 'vue'
import { EditorView, basicSetup } from 'codemirror'
import { EditorState, type Extension } from '@codemirror/state'
import { Decoration, MatchDecorator, ViewPlugin, keymap, type DecorationSet, type ViewUpdate } from '@codemirror/view'
import { html } from '@codemirror/lang-html'
import { HighlightStyle, syntaxHighlighting } from '@codemirror/language'
import { tags } from '@lezer/highlight'
import { autocompletion, type CompletionContext, type CompletionResult, type Completion } from '@codemirror/autocomplete'
import { setDiagnostics, type Diagnostic } from '@codemirror/lint'
import type { VariableGroup } from './templateVariables'

export interface EditorProblem {
  line: number // 1-based
  column: number // 1-based
  message: string
}

const props = defineProps<{
  modelValue: string
  variables: VariableGroup[]
  problems: EditorProblem[]
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'save'): void
}>()

const host = ref<HTMLElement | null>(null)
let view: EditorView | null = null

// ---------- [[ expression ]] highlighting ----------

const expressionMark = Decoration.mark({ class: 'cm-tpl-expr' })
const expressionMatcher = new MatchDecorator({
  regexp: /\[\[[\s\S]*?\]\]/g,
  decoration: () => expressionMark
})
const expressionHighlight = ViewPlugin.fromClass(
  class {
    decorations: DecorationSet
    constructor(v: EditorView) {
      this.decorations = expressionMatcher.createDeco(v)
    }
    update(update: ViewUpdate) {
      this.decorations = expressionMatcher.updateDeco(update, this.decorations)
    }
  },
  { decorations: instance => instance.decorations }
)

// ---------- autocomplete inside [[ ]] ----------

const keywords: Completion[] = [
  { label: 'for', type: 'keyword', detail: 'for x in list … end' },
  { label: 'if', type: 'keyword', detail: 'if condition … end' },
  { label: 'else', type: 'keyword' },
  { label: 'end', type: 'keyword' }
]

function templateCompletions(context: CompletionContext): CompletionResult | null {
  const line = context.state.doc.lineAt(context.pos)
  const before = line.text.slice(0, context.pos - line.from)
  // only inside an unclosed [[ on this line
  const open = before.lastIndexOf('[[')
  if (open < 0 || before.lastIndexOf(']]') > open) return null

  const word = context.matchBefore(/[\w.]*/)
  if (!word || (word.from === word.to && !context.explicit)) return null

  const names = new Map<string, Completion>()
  for (const group of props.variables) {
    for (const item of group.items) {
      if (!item.completion || names.has(item.completion)) continue
      names.set(item.completion, {
        label: item.completion,
        detail: group.title,
        info: item.description,
        type: /^[A-Z]/.test(item.completion) ? 'function' : 'variable'
      })
    }
  }
  return { from: word.from, options: [...names.values(), ...keywords], validFor: /^[\w.]*$/ }
}

// ---------- theme ----------
// Every colour is a CSS variable from style.css, so the editor follows the light/dark theme
// without being rebuilt.

const theme = EditorView.theme({
  '&': { height: '100%', fontSize: '13px', backgroundColor: 'var(--color-surface)', color: 'var(--color-text)' },
  '.cm-scroller': { fontFamily: 'var(--font-mono)', lineHeight: '1.55' },
  '.cm-content': { caretColor: 'var(--color-text)' },
  '.cm-cursor, .cm-dropCursor': { borderLeftColor: 'var(--color-text)' },
  '&.cm-focused > .cm-scroller > .cm-selectionLayer .cm-selectionBackground, .cm-selectionBackground, .cm-content ::selection':
    { backgroundColor: 'var(--code-selection)' },
  '.cm-gutters': {
    backgroundColor: 'var(--color-surface-muted)',
    color: 'var(--color-text-subtle)',
    borderRight: '1px solid var(--color-border)'
  },
  '.cm-activeLineGutter': { backgroundColor: 'var(--color-primary-soft)', color: 'var(--color-primary)' },
  '.cm-activeLine': { backgroundColor: 'var(--code-active-line)' },
  '&.cm-focused': { outline: 'none' },
  '.cm-matchingBracket': { backgroundColor: 'var(--color-primary-soft)', outline: '1px solid var(--color-primary-line)' },
  '.cm-tpl-expr': {
    backgroundColor: 'var(--code-expr-bg)',
    color: 'var(--code-expr)',
    borderRadius: '3px'
  },
  // panels and popups (search, autocomplete, lint) use CodeMirror's light defaults otherwise
  '.cm-panels': { backgroundColor: 'var(--color-surface-muted)', color: 'var(--color-text)' },
  '.cm-panels.cm-panels-top': { borderBottom: '1px solid var(--color-border)' },
  '.cm-panels.cm-panels-bottom': { borderTop: '1px solid var(--color-border)' },
  '.cm-textfield': {
    backgroundColor: 'var(--color-surface)',
    color: 'var(--color-text)',
    border: '1px solid var(--color-border-strong)',
    borderRadius: '4px'
  },
  '.cm-button': {
    backgroundImage: 'none',
    backgroundColor: 'var(--color-surface)',
    color: 'var(--color-text-secondary)',
    border: '1px solid var(--color-border-strong)'
  },
  '.cm-tooltip': {
    backgroundColor: 'var(--color-surface)',
    color: 'var(--color-text)',
    border: '1px solid var(--color-border)',
    borderRadius: '6px',
    boxShadow: 'var(--shadow-md)'
  },
  '.cm-tooltip-autocomplete ul li[aria-selected]': { backgroundColor: 'var(--color-primary)', color: 'var(--color-on-primary)' },
  '.cm-completionDetail': { color: 'var(--color-text-muted)' },
  '.cm-searchMatch': { backgroundColor: 'var(--color-warning-soft)', outline: '1px solid var(--color-warning-line)' },
  '.cm-searchMatch.cm-searchMatch-selected': { backgroundColor: 'var(--color-primary-soft)' }
})

// Syntax colours for HTML/CSS/JS inside templates, from the --code-* tokens
const highlighting = HighlightStyle.define([
  { tag: [tags.tagName, tags.angleBracket], color: 'var(--code-tag)' },
  { tag: tags.attributeName, color: 'var(--code-attr)' },
  { tag: [tags.attributeValue, tags.string], color: 'var(--code-string)' },
  { tag: tags.comment, color: 'var(--code-comment)', fontStyle: 'italic' },
  { tag: [tags.keyword, tags.operatorKeyword, tags.modifier], color: 'var(--code-keyword)' },
  { tag: [tags.number, tags.bool, tags.null, tags.unit], color: 'var(--code-number)' },
  { tag: [tags.propertyName, tags.className], color: 'var(--code-property)' },
  { tag: [tags.typeName, tags.variableName], color: 'var(--color-text)' },
  { tag: tags.invalid, color: 'var(--color-danger)' }
])

// ---------- lifecycle ----------

function extensions(): Extension[] {
  return [
    basicSetup,
    syntaxHighlighting(highlighting),
    html(),
    expressionHighlight,
    autocompletion({ override: [templateCompletions], activateOnTyping: true }),
    keymap.of([
      {
        key: 'Mod-s',
        preventDefault: true,
        run: () => {
          emit('save')
          return true
        }
      }
    ]),
    EditorView.lineWrapping,
    EditorView.updateListener.of(update => {
      if (update.docChanged) emit('update:modelValue', update.state.doc.toString())
    }),
    theme
  ]
}

onMounted(() => {
  view = new EditorView({
    parent: host.value!,
    state: EditorState.create({ doc: props.modelValue, extensions: extensions() })
  })
  applyProblems(props.problems)
})

onBeforeUnmount(() => {
  view?.destroy()
  view = null
})

// External changes (loading a template, "Load sample") replace the document
watch(
  () => props.modelValue,
  value => {
    if (view && value !== view.state.doc.toString()) {
      view.dispatch({ changes: { from: 0, to: view.state.doc.length, insert: value } })
    }
  }
)

watch(() => props.problems, applyProblems)

function applyProblems(problems: EditorProblem[]) {
  if (!view) return
  const doc = view.state.doc
  const diagnostics: Diagnostic[] = problems
    .filter(p => p.line >= 1 && p.line <= doc.lines)
    .map(p => {
      const line = doc.line(p.line)
      const from = Math.min(line.to, line.from + Math.max(0, p.column - 1))
      return { from, to: Math.max(from, line.to), severity: 'error', message: p.message }
    })
  view.dispatch(setDiagnostics(view.state, diagnostics))
}

// ---------- API for the page ----------

/** Inserts text at the cursor (replacing any selection); `$0` marks where the cursor ends up. */
function insert(text: string) {
  if (!view) return
  const marker = text.indexOf('$0')
  const clean = text.replace('$0', '')
  const { from, to } = view.state.selection.main
  view.dispatch({
    changes: { from, to, insert: clean },
    selection: { anchor: from + (marker >= 0 ? marker : clean.length) },
    scrollIntoView: true
  })
  view.focus()
}

function goToLine(lineNumber: number, column = 1) {
  if (!view) return
  const line = view.state.doc.line(Math.min(Math.max(1, lineNumber), view.state.doc.lines))
  const pos = Math.min(line.to, line.from + Math.max(0, column - 1))
  view.dispatch({ selection: { anchor: pos }, scrollIntoView: true })
  view.focus()
}

defineExpose({ insert, goToLine })
</script>

<style scoped>
.code-editor {
  height: 100%;
  min-height: 0;
  overflow: hidden;
}
</style>

import type { InjectionKey, Ref } from 'vue'

/** Collapse state shared by all rows of one BlockOutline (lets selection expand ancestors). */
export const outlineKey: InjectionKey<{ collapsed: Ref<Set<string>>; toggle: (id: string) => void }> =
	Symbol('outline')

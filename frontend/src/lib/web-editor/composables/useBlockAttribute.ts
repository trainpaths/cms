import { computed, type WritableComputedRef } from 'vue'
import { useEditorStore } from '../../../stores/editor'
import type { BlockInstance } from '../core/types'

type AttrValue = string | number | boolean
// fallback '' / false would otherwise infer literal types
type Widen<T> = T extends string ? string : T extends number ? number : boolean

/**
 * Writable computed over one block attribute. Writes go through `updateBlockAttributes`
 * (history + autosave), so toolbar and sidebar controls on the same attribute stay in sync.
 */
export function useBlockAttribute<T extends AttrValue>(
	block: () => BlockInstance,
	key: string,
	fallback: T,
): WritableComputedRef<Widen<T>> {
	const store = useEditorStore()
	return computed({
		get: () => ((block().attributes[key] as Widen<T>) || fallback) as Widen<T>,
		set: (value: Widen<T>) => store.updateBlockAttributes(block().id, { [key]: value }),
	})
}

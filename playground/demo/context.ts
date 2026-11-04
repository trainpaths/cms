import { inject, type InjectionKey, type Ref } from 'vue'

export interface DemoContext {
	/** language code of the site view (and the UI around it) */
	siteLang: Ref<string>
	changeLanguage(code: string): Promise<void>
	/** back to the seeded page (reloads the app) */
	reset(): void
}

export const DEMO: InjectionKey<DemoContext> = Symbol('demo')

export function useDemo(): DemoContext {
	const context = inject(DEMO)
	if (!context) throw new Error('useDemo() outside the demo app')
	return context
}

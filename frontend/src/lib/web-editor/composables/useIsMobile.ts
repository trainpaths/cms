import { readonly, ref } from 'vue'

/** Below Tailwind `md` (768px): editor sidebars become full-screen modals, block toolbar moves to a top bar. */
export const MOBILE_QUERY = '(max-width: 767.98px)'

// module scope: one listener shared by every caller
const mql = typeof window !== 'undefined' ? window.matchMedia(MOBILE_QUERY) : null
const isMobile = ref(mql?.matches ?? false)
mql?.addEventListener('change', (e) => (isMobile.value = e.matches))

export function useIsMobile() {
	return readonly(isMobile)
}

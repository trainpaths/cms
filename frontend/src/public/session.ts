import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { readAuthType, useAuthStore } from '../stores/auth'
import { safeRedirect } from './redirect'

/*
 * Customer session handling of the screens. Client-only (after mount): a screen's stored HTML is the same for every
 * visitor, so nothing that depends on the session or the query may render before hydration.
 */

/**
 * Login / register: `target()` = where to go after signing in (`?redirect` when safe, else home); `carry(path)` = a
 * link to the other guest screen that keeps `redirect`. Signed in already (other tab, back button) → straight on.
 */
export function useGuestScreen() {
	const route = useRoute()
	const router = useRouter()
	const authStore = useAuthStore()
	const mounted = ref(false)

	const target = () => safeRedirect(route.query.redirect) ?? '/'
	// no query until mounted: the server rendered the links without one
	const redirect = computed(() => (mounted.value ? safeRedirect(route.query.redirect) : null))
	const carry = (path: string) => (redirect.value ? `${path}?redirect=${encodeURIComponent(redirect.value)}` : path)

	onMounted(async () => {
		mounted.value = true
		if (readAuthType() === 'customer' && (await authStore.ensureSession())) await router.replace(target())
	})

	return { target, carry }
}

/** Account pages: true once a customer session is confirmed; without one → login, which comes back here. */
export function useCustomerSession() {
	const route = useRoute()
	const router = useRouter()
	const authStore = useAuthStore()
	const ready = ref(false)

	onMounted(async () => {
		if ((await authStore.ensureSession()) && authStore.authType === 'customer') ready.value = true
		else await router.replace({ name: 'login', query: { redirect: route.fullPath } })
	})

	return ready
}

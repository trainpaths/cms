import { client } from './api/client.gen'
import { toApiError } from './api-error'
import { useAuthStore } from './stores/auth'

// These answer 401 for bad credentials/cookies; refreshing and retrying them makes no sense
// (and retrying refresh itself would deadlock on the shared refresh promise).
const NO_REFRESH = /\/api\/auth\/(customer|staff)\/(login|register|refresh|logout)$/

// fetch() consumes the request body, so keep a copy to replay after a refresh.
const replayable = new WeakMap<Request, Request>()

/**
 * Bearer token, refresh-and-replay on 401, `ApiError`s. `onUnauthenticated` runs when a 401 survives the refresh
 * (session gone, auth already cleared): the app sends the user to its login screen.
 */
export function setupInterceptors(onUnauthenticated: () => void) {
	client.interceptors.request.use((request) => {
		const token = useAuthStore().accessToken
		if (token) request.headers.set('Authorization', `Bearer ${token}`)
		replayable.set(request, request.clone())
		return request
	})

	client.interceptors.response.use(async (response, request) => {
		if (response.status !== 401 || NO_REFRESH.test(new URL(request.url).pathname)) {
			return response
		}

		const authStore = useAuthStore()
		const retry = replayable.get(request)
		if (retry && (await authStore.refreshAuth())) {
			retry.headers.set('Authorization', `Bearer ${authStore.accessToken}`)
			return fetch(retry)
		}

		authStore.clearAuthData()
		onUnauthenticated()
		return response
	})

	client.interceptors.error.use(toApiError)
}

import type { Router } from 'vue-router'
import { client } from './api/client.gen'
import { toApiError } from './api-error'
import { useAuthStore } from './stores/auth'
import { loginRouteFor } from './router'

// These answer 401 for bad credentials/cookies; refreshing and retrying them makes no sense
// (and retrying refresh itself would deadlock on the shared refresh promise).
const NO_REFRESH = /\/api\/auth\/(customer|staff)\/(login|register|refresh|logout)$/

// fetch() consumes the request body, so keep a copy to replay after a refresh.
const replayable = new WeakMap<Request, Request>()

export function setupInterceptors(router: Router) {
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

		const type = authStore.authType
		authStore.clearAuthData()
		router.push(loginRouteFor(type))
		return response
	})

	client.interceptors.error.use(toApiError)
}

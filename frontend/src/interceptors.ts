import type { Router } from 'vue-router'
import { client } from './api/client.gen'
import { ApiError } from './api-error'
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

	// body is ProblemDetails (JSON), plain text, or a network TypeError (no response)
	client.interceptors.error.use((error, response) => {
		if (error instanceof ApiError) return error
		const status = response?.status ?? 0
		if (status === 413) return new ApiError('File exceeds 10 MB.', 413)
		const problem = typeof error === 'object' && error ? (error as { detail?: string; title?: string }) : {}
		const message = problem.detail ?? problem.title ?? (status ? `Request failed (${status})` : 'Network error')
		return new ApiError(message, status)
	})
}

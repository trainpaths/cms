import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { AuthResponse, UserInfo, StaffRegisterRequest } from '../api/types.gen'
import {
	postApiAuthCustomerRegister,
	postApiAuthCustomerLogin,
	postApiAuthCustomerLogout,
	postApiAuthCustomerRefresh,
	postApiAuthCustomerChangePassword,
	postApiAuthCustomerProfile,
	postApiAuthCustomerRequestVerification,
	postApiAuthCustomerConfirmEmail,
	postApiAuthCustomerForgotPassword,
	postApiAuthCustomerResetPassword,
	getApiAuthCustomerMe,
	postApiAuthStaffRegister,
	postApiAuthStaffLogin,
	postApiAuthStaffLogout,
	postApiAuthStaffRefresh,
	postApiAuthStaffChangePassword,
	postApiAuthStaffProfile,
	getApiAuthStaffMe,
} from '../api/sdk.gen'
import { ApiError, errorMessage } from '../api-error'

export type AuthType = 'customer' | 'staff'

// Only the (non-secret) user type is persisted. The access token lives in memory and the refresh
// token in an HttpOnly cookie, so neither is readable by injected scripts.
const AUTH_TYPE_KEY = 'auth_type'

const authEndpoints = {
	customer: {
		login: postApiAuthCustomerLogin,
		logout: postApiAuthCustomerLogout,
		refresh: postApiAuthCustomerRefresh,
		me: getApiAuthCustomerMe,
		changePassword: postApiAuthCustomerChangePassword,
		profile: postApiAuthCustomerProfile,
	},
	staff: {
		login: postApiAuthStaffLogin,
		logout: postApiAuthStaffLogout,
		refresh: postApiAuthStaffRefresh,
		me: getApiAuthStaffMe,
		changePassword: postApiAuthStaffChangePassword,
		profile: postApiAuthStaffProfile,
	},
} as const

function storage(action: (s: Storage) => void) {
	try {
		action(localStorage)
	} catch {
		// storage unavailable (private mode, blocked site data)
	}
}

/** Persisted user type of the last session (a hint: the session itself may have expired). */
export function readAuthType(): AuthType | null {
	try {
		const value = localStorage.getItem(AUTH_TYPE_KEY)
		return value === 'customer' || value === 'staff' ? value : null
	} catch {
		return null
	}
}

// Tokens were stored in localStorage by earlier versions.
storage((s) => {
	s.removeItem('auth_access_token')
	s.removeItem('auth_refresh_token')
})

export const useAuthStore = defineStore('auth', () => {
	const user = ref<UserInfo | null>(null)
	const accessToken = ref<string | null>(null)
	const accessTokenExpiresAt = ref<number | null>(null)
	const authType = ref<AuthType | null>(readAuthType())
	const error = ref<string | null>(null)
	const loading = ref(false)
	const redirectAfterLogin = ref<string | null>(null)

	let refreshing: Promise<boolean> | null = null

	const isAuthenticated = computed(() => !!accessToken.value)

	function hasValidAccessToken() {
		return !!accessToken.value && (accessTokenExpiresAt.value ?? Infinity) > Date.now()
	}

	function setSession(type: AuthType, response: AuthResponse) {
		accessToken.value = response.accessToken
		accessTokenExpiresAt.value = Date.parse(response.accessTokenExpiresAt)
		user.value = response.user
		authType.value = type
		storage((s) => s.setItem(AUTH_TYPE_KEY, type))
	}

	function clearAuthData() {
		accessToken.value = null
		accessTokenExpiresAt.value = null
		user.value = null
		authType.value = null
		storage((s) => s.removeItem(AUTH_TYPE_KEY))
	}

	/** Runs an API call with shared loading/error handling; resolves to whether it succeeded. */
	async function run<T>(action: () => Promise<{ data: T }>, onSuccess?: (data: T) => void): Promise<boolean> {
		error.value = null
		loading.value = true
		try {
			const { data } = await action()
			onSuccess?.(data)
			return true
		} catch (e) {
			error.value = errorMessage(e, 'An unexpected error occurred')
			return false
		} finally {
			loading.value = false
		}
	}

	function register(email: string, password: string, displayName?: string) {
		return run(
			() => postApiAuthCustomerRegister({ body: { email, password, displayName } }),
			(data) => setSession('customer', data),
		)
	}

	/** Creates a staff account (requires a super_admin session); doesn't change the current session. */
	function registerStaff(request: StaffRegisterRequest) {
		return run(() => postApiAuthStaffRegister({ body: request }))
	}

	function login(type: AuthType, email: string, password: string) {
		return run(
			() => authEndpoints[type].login({ body: { email, password } }),
			(data) => setSession(type, data),
		)
	}

	async function logout() {
		loading.value = true
		try {
			if (authType.value) await authEndpoints[authType.value].logout()
		} catch {
			// logout failure shouldn't block clearing local state
		} finally {
			clearAuthData()
			loading.value = false
		}
	}

	async function doRefresh(type: AuthType): Promise<boolean> {
		const refresh = authEndpoints[type].refresh
		try {
			const { data } = await refresh().catch(async (err: unknown) => {
				if (!(err instanceof ApiError && err.status === 401)) throw err
				// Another tab may have rotated the cookie at the same moment; retry once with the new one.
				await new Promise((resolve) => setTimeout(resolve, 300))
				return refresh()
			})
			setSession(type, data)
			return true
		} catch {
			clearAuthData()
			return false
		}
	}

	/** Exchanges the refresh cookie for a new access token; concurrent callers share one request. */
	function refreshAuth(): Promise<boolean> {
		if (!authType.value) return Promise.resolve(false)
		refreshing ??= doRefresh(authType.value).finally(() => {
			refreshing = null
		})
		return refreshing
	}

	/** True if there is a usable access token, refreshing it first when needed. */
	async function ensureSession(): Promise<boolean> {
		if (hasValidAccessToken()) return true
		return refreshAuth()
	}

	async function fetchCurrentUser() {
		const type = authType.value
		if (!type || !accessToken.value) return false
		return run(
			() => authEndpoints[type].me(),
			(data) => (user.value = data),
		)
	}

	async function changePassword(currentPassword: string, newPassword: string) {
		const type = authType.value
		if (!type) return false
		// The server revokes every session, including this one.
		return run(() => authEndpoints[type].changePassword({ body: { currentPassword, newPassword } }), clearAuthData)
	}

	async function updateProfile(displayName: string) {
		const type = authType.value
		if (!type) return false
		return run(
			() => authEndpoints[type].profile({ body: { displayName } }),
			(data) => (user.value = data),
		)
	}

	// Email verification + password reset (customer-only, no auth).

	function requestVerification(email: string) {
		return run(() => postApiAuthCustomerRequestVerification({ body: { email } }))
	}

	function confirmEmail(token: string) {
		return run(() => postApiAuthCustomerConfirmEmail({ body: { token } }))
	}

	function forgotPassword(email: string) {
		return run(() => postApiAuthCustomerForgotPassword({ body: { email } }))
	}

	function resetPassword(token: string, newPassword: string) {
		return run(() => postApiAuthCustomerResetPassword({ body: { token, newPassword } }))
	}

	function clearError() {
		error.value = null
	}

	function setRedirectAfterLogin(path: string | null) {
		redirectAfterLogin.value = path
	}

	function consumeRedirectAfterLogin(): string | null {
		const path = redirectAfterLogin.value
		redirectAfterLogin.value = null
		return path
	}

	return {
		user,
		accessToken,
		authType,
		isAuthenticated,
		error,
		loading,
		redirectAfterLogin,
		register,
		registerStaff,
		login,
		logout,
		fetchCurrentUser,
		refreshAuth,
		ensureSession,
		changePassword,
		updateProfile,
		requestVerification,
		confirmEmail,
		forgotPassword,
		resetPassword,
		clearError,
		clearAuthData,
		setRedirectAfterLogin,
		consumeRedirectAfterLogin,
	}
})

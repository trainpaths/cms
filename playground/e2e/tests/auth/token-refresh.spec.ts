import { test, expect, request, type Page } from '@playwright/test'
import { uniqueEmail, REFRESH_COOKIE, registerCustomer } from '../../fixtures/test-utils'

async function registerAndGetRefreshCookie(page: Page, prefix: string) {
	await registerCustomer(page, uniqueEmail(prefix))

	const cookie = (await page.context().cookies()).find((c) => c.name === REFRESH_COOKIE)
	expect(cookie).toBeDefined()
	return cookie!.value
}

// Fresh context per call, so only the explicitly passed cookie is sent.
async function refreshWith(baseURL: string, token: string) {
	const api = await request.newContext({ baseURL })
	try {
		const response = await api.post('/api/auth/customer/refresh', {
			headers: { Cookie: `${REFRESH_COOKIE}=${token}` },
		})
		return {
			status: response.status(),
			body: response.ok() ? await response.json() : null,
			setCookie: response.headers()['set-cookie'] ?? '',
		}
	} finally {
		await api.dispose()
	}
}

test.describe('Token Refresh', () => {
	test('refresh cookie is rotated and not exposed in the body', async ({ page, baseURL }) => {
		const original = await registerAndGetRefreshCookie(page, 'refresh')

		const result = await refreshWith(baseURL!, original)

		expect(result.status).toBe(200)
		expect(result.body.accessToken).toBeTruthy()
		expect(result.body.refreshToken).toBeUndefined()
		expect(result.setCookie).toContain(`${REFRESH_COOKIE}=`)
		expect(result.setCookie.toLowerCase()).toContain('httponly')
		expect(result.setCookie).not.toContain(`${REFRESH_COOKIE}=${original};`)
	})

	test('used refresh token becomes invalid (rotation)', async ({ page, baseURL }) => {
		const original = await registerAndGetRefreshCookie(page, 'rotate')

		expect((await refreshWith(baseURL!, original)).status).toBe(200)
		expect((await refreshWith(baseURL!, original)).status).toBe(401)
	})

	test('session is restored from the cookie after navigation reload', async ({ page }) => {
		await registerAndGetRefreshCookie(page, 'transparent')

		// the account page's heading is in the stored HTML; the button needs the restored session
		await page.goto('/user/profile')
		await expect(page.getByRole('button', { name: 'Logout' })).toBeVisible()
	})
})

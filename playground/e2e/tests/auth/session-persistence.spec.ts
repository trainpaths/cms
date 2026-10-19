import { test, expect } from '@playwright/test'
import { uniqueEmail, REFRESH_COOKIE, registerCustomer } from '../../fixtures/test-utils'

test.describe('Session Persistence', () => {
	test('authenticated state persists across page reload', async ({ page }) => {
		const email = uniqueEmail('persist')
		await registerCustomer(page, email)

		await page.goto('/user/profile')
		await page.reload()

		await expect(page.getByText(email.toLowerCase())).toBeVisible()
	})

	test('refresh token is an HttpOnly cookie and no token is in localStorage', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('tokens'))

		const cookie = (await page.context().cookies()).find((c) => c.name === REFRESH_COOKIE)
		expect(cookie?.httpOnly).toBe(true)
		expect(cookie?.sameSite).toBe('Strict')

		const stored = await page.evaluate(() => ({ ...localStorage }))
		expect(Object.keys(stored)).toEqual(['auth_type'])
		expect(await page.evaluate(() => document.cookie)).not.toContain(REFRESH_COOKIE)
	})

	test('user info fetched on profile page', async ({ page }) => {
		const email = uniqueEmail('fetch-me')
		const displayName = 'Fetch Test User'
		await registerCustomer(page, email, { displayName })

		await page.goto('/user/profile')

		await expect(page.getByText(email.toLowerCase())).toBeVisible()
		await expect(page.getByText(displayName)).toBeVisible()
	})
})

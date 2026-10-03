import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword, REFRESH_COOKIE } from '../../fixtures/test-utils'

test.describe('Session Persistence', () => {
	test('authenticated state persists across page reload', async ({ page }) => {
		const email = uniqueEmail('persist')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.reload()

		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
	})

	test('refresh token is an HttpOnly cookie and no token is in localStorage', async ({ page }) => {
		const email = uniqueEmail('tokens')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

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

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByPlaceholder('Display name (optional)').fill(displayName)
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')

		await expect(page.getByText(email.toLowerCase())).toBeVisible()
		await expect(page.getByText(displayName)).toBeVisible()
	})
})

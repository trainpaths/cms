import { test, expect } from '../../fixtures/auth.fixture'
import { REFRESH_COOKIE } from '../../fixtures/test-utils'

test.describe('Logout', () => {
	test('logout goes home and ends the session', async ({ authenticatedPage: page }) => {
		await page.goto('/user/profile')
		await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()

		await page.getByRole('button', { name: 'Logout' }).click()

		await expect(page).toHaveURL('/')
		await page.goto('/user/profile')
		await expect(page).toHaveURL(/\/login\?redirect=/)
	})

	test('logout clears the refresh cookie', async ({ authenticatedPage: page }) => {
		const refreshCookie = async () => (await page.context().cookies()).find((c) => c.name === REFRESH_COOKIE)

		await page.goto('/user/profile')
		await expect(page.getByRole('button', { name: 'Logout' })).toBeVisible()
		expect(await refreshCookie()).toBeDefined()

		await page.getByRole('button', { name: 'Logout' }).click()
		await expect(page).toHaveURL('/')

		expect(await refreshCookie()).toBeUndefined()
	})
})

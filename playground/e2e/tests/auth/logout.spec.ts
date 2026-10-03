import { test, expect } from '../../fixtures/auth.fixture'
import { REFRESH_COOKIE } from '../../fixtures/test-utils'

test.describe('Logout', () => {
	test('logout clears authenticated state', async ({ authenticatedPage }) => {
		await authenticatedPage.goto('/profile')
		await expect(authenticatedPage.getByRole('heading', { name: 'Profile' })).toBeVisible()

		await authenticatedPage.getByRole('button', { name: 'Logout' }).click()

		await expect(authenticatedPage.getByRole('heading', { name: 'Login' })).toBeVisible()
		await expect(authenticatedPage.getByPlaceholder('Email')).toBeVisible()
	})

	test('logout clears the refresh cookie', async ({ authenticatedPage }) => {
		const refreshCookie = async () =>
			(await authenticatedPage.context().cookies()).find((c) => c.name === REFRESH_COOKIE)

		await authenticatedPage.goto('/profile')
		await expect(authenticatedPage.getByRole('heading', { name: 'Profile' })).toBeVisible()
		expect(await refreshCookie()).toBeDefined()

		await authenticatedPage.getByRole('button', { name: 'Logout' }).click()
		await expect(authenticatedPage.getByRole('heading', { name: 'Login' })).toBeVisible()

		expect(await refreshCookie()).toBeUndefined()

		await authenticatedPage.reload()
		await expect(authenticatedPage.getByRole('heading', { name: 'Login' })).toBeVisible()
	})
})

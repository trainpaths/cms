import { test as base, type Page, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from './test-utils'

type AuthFixtures = {
	authenticatedPage: Page
	testUser: { email: string; password: string }
}

export const test = base.extend<AuthFixtures>({
	testUser: async ({}, use) => {
		await use({
			email: uniqueEmail('fixture'),
			password: testPassword(),
		})
	},

	authenticatedPage: async ({ page, testUser }, use) => {
		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(testUser.email)
		await page.getByPlaceholder('Password').fill(testUser.password)
		await page.getByPlaceholder('Display name (optional)').fill('E2E Test User')
		await page.getByRole('button', { name: /Register as customer/i }).click()

		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await use(page)

		const logoutButton = page.getByRole('button', { name: 'Logout' })
		if (await logoutButton.isVisible().catch(() => false)) {
			await logoutButton.click()
		}
	},
})

export { expect } from '@playwright/test'

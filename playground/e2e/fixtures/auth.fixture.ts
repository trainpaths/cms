import { test as base, type Page } from '@playwright/test'
import { uniqueEmail, testPassword, registerCustomer } from './test-utils'

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
		await registerCustomer(page, testUser.email, { password: testUser.password, displayName: 'E2E Test User' })

		await use(page)

		const logoutButton = page.getByRole('button', { name: 'Logout' })
		if (await logoutButton.isVisible().catch(() => false)) {
			await logoutButton.click()
		}
	},
})

export { expect } from '@playwright/test'

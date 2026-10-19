import { test, expect } from '../fixtures/staff.fixture'

test.describe('Dashboard', () => {
	test('Pages card leads to the pages list', async ({ staffPage: page }) => {
		await expect(page.getByRole('link', { name: 'View Profile' })).toHaveCount(0)
		await page.getByTestId('dashboard-pages-link').click()
		await expect(page).toHaveURL('/admin/pages')
	})

	test('Log Out button logs out', async ({ staffPage: page }) => {
		await page.getByTestId('dashboard-logout').click()
		await expect(page).toHaveURL('/admin/login')
		await page.goto('/admin')
		await expect(page).toHaveURL('/admin/login')
	})
})

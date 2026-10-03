import { test, expect } from '../fixtures/staff.fixture'

test.describe('App navigation', () => {
	test('top nav links for staff', async ({ staffPage: page }) => {
		const nav = page.getByRole('navigation')
		for (const name of ['Dashboard', 'Pages', 'Media', 'Configuration', 'User']) {
			await expect(nav.getByRole('link', { name, exact: true })).toBeVisible()
		}
		await nav.getByRole('link', { name: 'Configuration' }).click()
		await expect(page.getByRole('heading', { name: 'Configuration' })).toBeVisible()
		await nav.getByRole('link', { name: 'User' }).click()
		await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()
	})

	test('burger menu on small screens', async ({ staffPage: page }) => {
		await page.setViewportSize({ width: 390, height: 800 })
		const nav = page.getByRole('navigation')
		await expect(nav.getByRole('link', { name: 'Pages', exact: true })).toBeHidden()
		await page.getByRole('button', { name: 'Open menu' }).click()
		await page.locator('#app-nav-menu').getByRole('link', { name: 'Media' }).click()
		await expect(page.getByRole('heading', { name: 'Media' })).toBeVisible()
		await expect(page.locator('#app-nav-menu')).toHaveCount(0)
	})

	test('no nav in the editor', async ({ staffPage: page }) => {
		await page.goto('/admin/pages')
		await expect(page.getByRole('navigation')).toBeVisible()
		await page.getByRole('button', { name: '+ New Page' }).click()
		await page.getByPlaceholder('Page title...').fill(`Nav ${crypto.randomUUID().slice(0, 8)}`)
		await page.getByRole('button', { name: 'Create', exact: true }).click()
		await expect(page.getByRole('button', { name: 'Back', exact: true })).toBeVisible()
		await expect(page.getByRole('navigation')).toHaveCount(0)
	})
})

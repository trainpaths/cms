import { test, expect } from '../fixtures/staff.fixture'

test.describe('App navigation', () => {
	test('sidebar links for staff', async ({ staffPage: page }) => {
		const nav = page.getByRole('navigation', { name: 'Admin' })
		for (const name of ['Dashboard', 'Pages', 'Menus', 'Media', 'Configuration']) {
			await expect(nav.getByRole('link', { name, exact: true })).toBeVisible()
		}
		// sidebar sits left of the content, full height
		const box = await page.locator('#app-nav-menu').boundingBox()
		expect(box?.x).toBe(0)
		expect(box?.y).toBe(0)
		await nav.getByRole('link', { name: 'Configuration' }).click()
		await expect(page.getByRole('heading', { name: 'Configuration' })).toBeVisible()
		await page.getByRole('link', { name: 'User', exact: true }).click()
		await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()
	})

	test('exit icon logs out', async ({ staffPage: page }) => {
		await page.getByRole('button', { name: 'Log out' }).click()
		await expect(page).toHaveURL('/admin/login')
		await page.goto('/admin')
		await expect(page).toHaveURL('/admin/login')
	})

	test('burger opens a full-screen menu on small screens', async ({ staffPage: page }) => {
		await page.setViewportSize({ width: 390, height: 800 })
		const menu = page.locator('#app-nav-menu')
		await expect(menu).toBeHidden()
		await page.getByRole('button', { name: 'Open menu' }).click()
		// overlays the page below the 56px bar instead of pushing it down
		const box = await menu.boundingBox()
		expect(box).toMatchObject({ x: 0, y: 56, width: 390, height: 800 - 56 })
		await expect(menu.getByRole('button', { name: 'Log out' })).toBeVisible()
		await menu.getByRole('link', { name: 'Media' }).click()
		await expect(page.getByRole('heading', { name: 'Media' })).toBeVisible()
		await expect(menu).toBeHidden()
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

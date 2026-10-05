import { test, expect } from '../../fixtures/staff.fixture'

test.describe('Admin bar on the public site', () => {
	test('staff see it: link to the admin, log out hides it', async ({ staffPage: page }) => {
		await page.goto('/')
		const bar = page.getByTestId('admin-bar')
		await expect(bar).toBeVisible()
		await bar.getByRole('link', { name: 'Admin' }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/')
		await bar.getByTestId('admin-bar-logout').click()
		await expect(bar).toHaveCount(0)
		await page.reload()
		await expect(page.getByRole('heading', { name: 'Home', level: 1 })).toBeVisible()
		await expect(bar).toHaveCount(0)
		await page.goto('/admin')
		await expect(page).toHaveURL('/admin/login')
	})

	test('visitors do not see it', async ({ page }) => {
		await page.goto('/')
		await expect(page.getByRole('heading', { name: 'Home', level: 1 })).toBeVisible()
		await expect(page.getByTestId('admin-bar')).toHaveCount(0)
	})
})

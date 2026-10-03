import { test, expect } from '@playwright/test'

// Relies on the pages seeded on a fresh install (playground/cms.config.json `pages`).
test.describe('Public site', () => {
	test('home page is served at / and /home redirects there', async ({ page }) => {
		await page.goto('/')
		await expect(page.getByRole('heading', { name: 'Home', level: 1 })).toBeVisible()
		await page.goto('/home')
		await expect(page).toHaveURL('/')
	})

	test('footer links to the privacy policy and legal pages', async ({ page }) => {
		await page.goto('/')
		const links = page.getByTestId('site-footer-link')
		await expect(links).toHaveText(['Privacy Policy', 'Legal'])
		await links.filter({ hasText: 'Privacy Policy' }).click()
		await expect(page).toHaveURL('/privacy-policy')
		await expect(page.getByRole('heading', { name: 'Privacy Policy', level: 1 })).toBeVisible()
		await page.getByTestId('site-footer-link').filter({ hasText: 'Legal' }).click()
		await expect(page).toHaveURL('/legal')
		await expect(page.getByRole('heading', { name: 'Legal', level: 1 })).toBeVisible()
	})
})

test.describe('Public nav on auth screens', () => {
	test('customer auth pages show the public header, staff login does not', async ({ page }) => {
		await page.goto('/')
		const headers = await page.getByTestId('site-header').count()

		for (const path of ['/login', '/register', '/forgot-password']) {
			await page.goto(path)
			await expect(page.getByTestId('site-header')).toHaveCount(headers)
		}

		await page.goto('/admin/login')
		await expect(page.getByRole('heading', { name: 'Admin Login' })).toBeVisible()
		await expect(page.getByTestId('site-header')).toHaveCount(0)
	})
})

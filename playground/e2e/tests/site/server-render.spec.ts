import { test, expect } from '@playwright/test'

// Public pages are pre-rendered by the renderer service and served as HTML (PublicHtmlController).
test.describe('Server-rendered public pages', () => {
	test.describe('without JavaScript', () => {
		test.use({ javaScriptEnabled: false })

		test('content, title and link-preview tags are in the HTML', async ({ page }) => {
			await page.goto('/privacy-policy')
			await expect(page.getByRole('heading', { name: 'Privacy Policy', level: 1 })).toBeVisible()
			await expect(page).toHaveTitle('Privacy Policy')
			await expect(page.locator('meta[property="og:title"]')).toHaveAttribute('content', 'Privacy Policy')
			await expect(page.locator('link[rel="canonical"]')).toHaveAttribute('href', /\/privacy-policy$/)
		})

		test('unknown pages are a real 404', async ({ page }) => {
			const response = await page.goto('/no-such-page-e2e')
			expect(response?.status()).toBe(404)
			await expect(page.getByText('Page not found')).toBeVisible()
		})
	})

	test('hydrates without mismatches and navigates client-side', async ({ page }) => {
		const problems: string[] = []
		page.on('console', (msg) => {
			if (msg.type() === 'error' || /hydration/i.test(msg.text())) problems.push(msg.text())
		})
		await page.goto('/')
		await expect(page.getByRole('heading', { name: 'Home', level: 1 })).toBeVisible()

		// a document request would mean a full page load
		const documents: string[] = []
		page.on('request', (req) => {
			if (req.resourceType() === 'document') documents.push(req.url())
		})
		await page.getByTestId('site-footer-link').filter({ hasText: 'Legal' }).click()
		await expect(page.getByRole('heading', { name: 'Legal', level: 1 })).toBeVisible()
		await expect(page).toHaveTitle('Legal')
		expect(documents).toEqual([])
		expect(problems).toEqual([])
	})

	test('app routes still get the admin SPA', async ({ page }) => {
		await page.goto('/admin/login')
		await expect(page.getByRole('heading', { name: 'Admin Login' })).toBeVisible()
	})
})

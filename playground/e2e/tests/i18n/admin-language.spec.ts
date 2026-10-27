import { test, expect } from '@playwright/test'

// the playground is localized (cms.config.json site.lang: en-US + de-CH, fr-CH, it-CH)
test.describe('Admin language', () => {
	test('opens in the default language and switches in place, remembered in the cms_lang cookie', async ({
		page,
		context,
	}) => {
		await page.goto('/admin/login')
		await expect(page.getByRole('heading', { name: 'Admin Login' })).toBeVisible()
		await expect(page.locator('html')).toHaveAttribute('lang', 'en-US')

		await page.getByTestId('language-dropdown').getByRole('button', { name: 'English' }).click()
		await page.getByRole('menuitem', { name: 'Deutsch' }).click()

		await expect(page.getByRole('heading', { name: 'Anmeldung Verwaltung' })).toBeVisible()
		await expect(page.getByPlaceholder('Passwort')).toBeVisible()
		await expect(page.locator('html')).toHaveAttribute('lang', 'de-CH')
		const cookie = (await context.cookies()).find((c) => c.name === 'cms_lang')
		expect(cookie?.value).toBe('de')

		await page.reload()
		await expect(page.getByRole('heading', { name: 'Anmeldung Verwaltung' })).toBeVisible()
	})

	test('ignores a cookie naming a language the site does not have', async ({ page, context, baseURL }) => {
		await context.addCookies([{ name: 'cms_lang', value: 'es', url: baseURL! }])
		await page.goto('/admin/login')
		await expect(page.getByRole('heading', { name: 'Admin Login' })).toBeVisible()
	})
})

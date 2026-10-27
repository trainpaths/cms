import { test, expect } from '@playwright/test'

// the playground is localized (cms.config.json site.lang: en-US + de-CH, fr-CH, it-CH): one URL per language,
// default unprefixed. Other specs never set cms_lang, so they always see the default language.
test.describe('Public site languages', () => {
	test('footer switch loads the same page in German, remembers it, unprefixed URLs then redirect', async ({
		page,
		context,
	}) => {
		const warnings: string[] = []
		page.on('console', (msg) => {
			if (msg.text().includes('Hydration')) warnings.push(msg.text())
		})

		await page.goto('/privacy-policy')
		await expect(page.locator('html')).toHaveAttribute('lang', 'en-US')
		const footer = page.getByTestId('site-footer')
		await footer.getByTestId('language-dropdown').getByRole('button', { name: 'English' }).click()
		await page.getByRole('menuitem', { name: 'Deutsch' }).click()

		await expect(page).toHaveURL(/\/de\/privacy-policy$/)
		await expect(page.locator('html')).toHaveAttribute('lang', 'de-CH')
		await expect(footer.getByRole('navigation', { name: 'Rechtliches' })).toBeVisible()
		await expect(footer.getByTestId('language-dropdown').getByRole('button', { name: 'Deutsch' })).toBeVisible()
		expect((await context.cookies()).find((c) => c.name === 'cms_lang')?.value).toBe('de')

		await page.goto('/privacy-policy')
		await expect(page).toHaveURL(/\/de\/privacy-policy$/)

		// router links keep the prefix
		await page.goto('/de/login')
		await expect(page.getByRole('heading', { name: 'Anmelden' })).toBeVisible()
		await expect(page.getByPlaceholder('Passwort')).toBeVisible()
		await page.getByRole('link', { name: 'Registrieren' }).click()
		await expect(page).toHaveURL(/\/de\/register/)

		expect(warnings).toEqual([])
	})

	test('server-rendered HTML per language', async ({ request }) => {
		const french = await (await request.get('/fr/privacy-policy')).text()
		expect(french).toContain('lang="fr-CH"')
		expect(french).toContain('Mentions légales')
		expect(french).toContain('hreflang="de-CH" href="')

		const english = await request.get('/en/privacy-policy', { maxRedirects: 0 })
		expect(english.status()).toBe(301)
		expect(english.headers().location).toBe('/privacy-policy')
	})
})

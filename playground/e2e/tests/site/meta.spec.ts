import { test, expect, createPage, type Page } from '../../fixtures/staff.fixture'

// Page meta (PageSettings) + site meta (firm name in Configuration, lang from cms.config.json) in the rendered <head>.

async function setFirmName(page: Page, name: string) {
	await page.goto('/admin/configuration')
	await page.getByTestId('config-field-firmName').fill(name)
	if (await page.getByTestId('config-save').isEnabled()) {
		await page.getByTestId('config-save').click()
		await expect(page.getByText('Configuration saved')).toBeVisible()
	}
}

test('meta title + description set in the editor end up in the server-rendered head', async ({
	staffPage: page,
	request,
}) => {
	await setFirmName(page, 'ACME Meta')
	try {
		await createPage(page, 'Team page')
		const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`
		await page.getByTestId('page-slug').fill(slug)
		await page.getByTestId('page-slug').press('Enter')

		await expect(page.getByTestId('page-tab-title')).toHaveText('Tab: ACME Meta')
		await page.getByTestId('page-meta-title').fill('Our team')
		await expect(page.getByTestId('page-tab-title')).toHaveText('Tab: Our team - ACME Meta')
		await page.getByTestId('page-meta-description').fill('Who builds ACME.')
		await page.getByTestId('page-meta-description').blur()
		await page.getByTestId('publish-toggle').click()
		await expect(page.getByTestId('publish-toggle')).toHaveText('Unpublish')

		// rendered HTML replaces the shell once the renderer ran
		await expect
			.poll(async () => (await (await request.get(`/${slug}`)).text()).includes('<title>Our team - ACME Meta</title>'), {
				timeout: 15_000,
			})
			.toBe(true)
		const html = await (await request.get(`/${slug}`)).text()
		expect(html).toContain('<meta name="description" content="Who builds ACME.">')
		expect(html).toContain('<meta property="og:title" content="Our team">')
		expect(html).toContain('<meta property="og:site_name" content="ACME Meta">')
		expect(html).toMatch(/<html[^>]* lang="en"/)

		// reload: meta persisted
		await page.reload()
		await expect(page.getByTestId('page-meta-title')).toHaveValue('Our team')
		await expect(page.getByTestId('page-meta-description')).toHaveValue('Who builds ACME.')
	} finally {
		// other specs expect page titles without a firm name
		await setFirmName(page, '')
	}
})

import { test, expect, createPage, insertBlock } from '../../fixtures/staff.fixture'

// What the playground adds as an instance (playground/src/, cms.config.json): the extension points a client site uses.

test('instance block: inserted in the editor, server-rendered on the public site', async ({ staffPage: page, request }) => {
	await createPage(page)
	const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`
	await page.getByTestId('page-slug').fill(slug)
	await page.getByTestId('page-slug').press('Enter')

	await insertBlock(page, 'Callout')
	await page.getByTestId('callout-input').fill('Instance block text')
	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Unpublish')

	// the renderer's SSR bundle carries the instance block (rendered HTML replaces the shell once ready)
	await expect
		.poll(async () => (await (await request.get(`/${slug}`)).text()).includes('Instance block text'), { timeout: 15_000 })
		.toBe(true)
	const html = await (await request.get(`/${slug}`)).text()
	expect(html).toContain('data-testid="callout"')
	expect(html).toContain('class="site-theme')
})

test('component override: the playground NotFound wraps the original', async ({ page }) => {
	const response = await page.goto('/no-such-page-instance')
	expect(response?.status()).toBe(404)
	await expect(page.getByText('Page not found')).toBeVisible()
	await expect(page.getByTestId('playground-not-found')).toBeVisible()
})

test('template page: rendered by the instance template, locked in the admin', async ({ staffPage: page }) => {
	await page.goto('/imprint')
	await expect(page.getByRole('heading', { name: 'Imprint', level: 1 })).toBeVisible()
	await expect(page.getByText('Responsible for the content of this website:')).toBeVisible()
	await expect(page.getByTestId('imprint-details')).toBeVisible()

	await page.goto('/admin/pages')
	const row = page.getByTestId('page-row').filter({ hasText: '/imprint' })
	await expect(row.getByTestId('page-delete')).toBeDisabled()

	await row.getByTestId('page-edit').click()
	await expect(page.getByTestId('page-edit-slug')).toBeDisabled()
	await expect(page.getByText('Fixed by the site configuration')).toBeVisible()
})

test('template page in the editor: blocks stay editable, slug is fixed', async ({ staffPage: page }) => {
	await page.goto('/admin/pages')
	await page.getByTestId('page-row').filter({ hasText: '/imprint' }).getByRole('link').first().click()
	await expect(page.getByTestId('page-slug')).toBeDisabled()
	await expect(page.locator('textarea').first()).toHaveValue('Responsible for the content of this website:')
	await expect(page.getByTestId('template-page-notice')).toHaveCount(0)
})

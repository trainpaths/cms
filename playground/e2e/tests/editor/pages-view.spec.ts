import { test, expect, createPage } from '../../fixtures/staff.fixture'

test('pages list: View opens the preview for drafts, the public page once published', async ({ staffPage: page }) => {
	const title = await createPage(page)
	const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`
	await page.getByTestId('page-slug').fill(slug)
	await page.getByTestId('page-slug').press('Enter')
	const editorUrl = page.url()

	await page.goto('/admin/pages')
	const row = page.getByTestId('page-row').filter({ hasText: title })
	await expect(row.getByTestId('page-view')).toHaveAttribute('href', `${new URL(editorUrl).pathname}/preview`)

	await page.goto(editorUrl)
	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Unpublish')
	await page.goto('/admin/pages')
	const view = row.getByTestId('page-view')
	await expect(view).toHaveAttribute('href', `/${slug}`)

	const [tab] = await Promise.all([page.waitForEvent('popup'), view.click()])
	await expect(tab.getByRole('heading', { name: title, level: 1 })).toBeVisible()
})

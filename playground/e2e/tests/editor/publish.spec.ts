import { test, expect, createPage, insertBlock } from '../../fixtures/staff.fixture'

test('draft is hidden, published page renders publicly at /:slug', async ({ staffPage: page, browser }) => {
	const title = await createPage(page)
	const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`

	await page.getByTestId('page-slug').fill(slug)
	await page.getByTestId('page-slug').press('Enter')
	await insertBlock(page, 'Heading')
	await page.getByPlaceholder('Click to add heading...').fill('Public heading')

	// Anonymous visitor in a separate context (no session cookie).
	const visitor = await (await browser.newContext()).newPage()
	await visitor.goto(`/${slug}`)
	await expect(visitor.getByText('Page not found')).toBeVisible()

	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Unpublish')

	await visitor.goto(`/${slug}`)
	await expect(visitor.getByRole('heading', { name: title })).toBeVisible()
	await expect(visitor.getByRole('heading', { name: 'Public heading' })).toBeVisible()

	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Publish')
	await visitor.goto(`/${slug}`)
	await expect(visitor.getByText('Page not found')).toBeVisible()
})

test('reserved slugs are rejected', async ({ staffPage: page }) => {
	await createPage(page)
	await page.getByTestId('page-slug').fill('login')
	await page.getByTestId('page-slug').press('Enter')
	await expect(page.getByText("Slug 'login' is reserved.")).toBeVisible()
})

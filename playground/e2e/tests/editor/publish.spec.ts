import { test, expect, createPage, insertBlock, publish, unpublish } from '../../fixtures/staff.fixture'

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

	await publish(page)

	await visitor.goto(`/${slug}`)
	await expect(visitor.getByRole('heading', { name: title })).toBeVisible()
	await expect(visitor.getByRole('heading', { name: 'Public heading' })).toBeVisible()

	await unpublish(page)
	await visitor.goto(`/${slug}`)
	await expect(visitor.getByText('Page not found')).toBeVisible()
})

test('saving never publishes: visitors see the live copy until "Publish changes"', async ({
	staffPage: page,
	browser,
}) => {
	await createPage(page)
	const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`
	await page.getByTestId('page-slug').fill(slug)
	await page.getByTestId('page-slug').press('Enter')
	await insertBlock(page, 'Heading')
	const heading = page.getByPlaceholder('Click to add heading...')
	await heading.fill('First version')
	// the inserted block switched the right sidebar to its Block tab; typing in it keeps the selection
	await page.getByTestId('sidebar-tab-page').click()

	const save = page.getByTestId('page-save')
	const publishButton = page.getByTestId('page-publish')
	const status = page.getByTestId('page-status')

	// unsaved: Save on, Publish off ("Save first"), no Unpublish
	await expect(save).toBeEnabled()
	await expect(publishButton).toBeDisabled()
	await expect(publishButton).toHaveAttribute('title', 'Save first')
	await expect(page.getByTestId('page-unpublish')).toHaveCount(0)
	await expect(status).toHaveText('Draft')

	await save.click()
	await expect(save).toBeDisabled()
	await expect(publishButton).toBeEnabled()
	await publish(page)
	await expect(status).toHaveText('Published')
	await expect(publishButton).toBeDisabled()
	await expect(publishButton).toHaveAttribute('title', 'No changes to publish')

	const visitor = await (await browser.newContext()).newPage()
	await visitor.goto(`/${slug}`)
	await expect(visitor.getByRole('heading', { name: 'First version' })).toBeVisible()

	// save an edit: the public page keeps the published version
	await heading.fill('Second version')
	await expect(publishButton).toBeDisabled()
	const saved = page.waitForResponse((r) => r.url().includes('/api/pages/') && r.request().method() === 'PUT')
	await save.click()
	await saved
	await expect(status).toHaveText('Published · unpublished changes')
	await expect(publishButton).toBeEnabled()
	await expect(publishButton).toHaveText('Publish changes')

	await visitor.goto(`/${slug}`)
	await expect(visitor.getByRole('heading', { name: 'First version' })).toBeVisible()
	await expect(visitor.getByRole('heading', { name: 'Second version' })).toHaveCount(0)

	// undo back to the saved state = clean again
	await heading.fill('Third version')
	await expect(save).toBeEnabled()
	await heading.fill('Second version')
	await expect(save).toBeDisabled()

	await publishButton.click()
	await page.getByTestId('confirm-ok').click()
	await expect(status).toHaveText('Published')
	await expect(publishButton).toBeDisabled()
	await expect
		.poll(async () => {
			await visitor.goto(`/${slug}`)
			return visitor.getByRole('heading', { name: 'Second version' }).count()
		})
		.toBe(1)
	await visitor.close()
})

test('< md: Publish is a globe icon with the same rules, Unpublish lives in the Page tab', async ({
	staffPage: page,
}) => {
	await page.setViewportSize({ width: 375, height: 700 })
	await createPage(page)
	await page.reload()

	const icon = page.getByTestId('page-publish-icon')
	await expect(icon).toBeVisible()
	await expect(page.getByTestId('page-publish')).toBeHidden()
	await expect(icon).toBeEnabled()
	await expect(icon).toHaveAttribute('title', 'Publish')
	await expect(page.getByRole('button', { name: 'Back', exact: true })).toBeVisible()

	await icon.click()
	await page.getByTestId('confirm-ok').click()
	await expect(icon).toBeDisabled()
	await expect(icon).toHaveAttribute('title', 'No changes to publish')
	await expect(page.getByTestId('page-unpublish')).toBeHidden()

	await page.getByTestId('toggle-right-sidebar').click()
	await expect(page.getByTestId('page-status')).toHaveText('Published')
	await expect(page.getByTestId('settings-publish')).toHaveText('Publish changes')
	await expect(page.getByTestId('settings-publish')).toBeDisabled()
	await page.getByTestId('settings-unpublish').click()
	await page.getByTestId('confirm-ok').click()
	await expect(page.getByTestId('page-status')).toHaveText('Draft')
	await expect(page.getByTestId('settings-unpublish')).toHaveCount(0)
	await expect(icon).toBeEnabled()
})

test('Publish and Unpublish ask first; Cancel changes nothing', async ({ staffPage: page }) => {
	await createPage(page)
	await page.getByTestId('sidebar-tab-page').click()
	const status = page.getByTestId('page-status')
	const dialog = page.getByTestId('confirm-dialog')

	await page.getByTestId('page-publish').click()
	await expect(dialog).toContainText('Publish this page?')
	await page.getByTestId('confirm-cancel').click()
	await expect(dialog).toBeHidden()
	await expect(status).toHaveText('Draft')

	await publish(page)
	await expect(status).toHaveText('Published')

	await page.getByTestId('page-unpublish').click()
	await expect(dialog).toContainText('Unpublish this page?')
	await page.keyboard.press('Escape')
	await expect(dialog).toBeHidden()
	await expect(status).toHaveText('Published')
	await unpublish(page)
	await expect(status).toHaveText('Draft')
})

test('reserved slugs are rejected', async ({ staffPage: page }) => {
	await createPage(page)
	await page.getByTestId('page-slug').fill('login')
	await page.getByTestId('page-slug').press('Enter')
	await expect(page.getByText("Slug 'login' is reserved.")).toBeVisible()
})

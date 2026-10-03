import { test, expect, createPage, insertBlock, uploadImage } from '../../fixtures/staff.fixture'

test('media library: upload, edit alt, delete', async ({ staffPage: page }) => {
	await page.goto('/admin/media')
	await expect(page.getByRole('heading', { name: 'Media' })).toBeVisible()

	const name = await uploadImage(page.locator('body'))
	const item = page.getByTestId('media-item').filter({ hasText: name })
	await expect(item).toContainText('No alt text')

	// fresh upload is selected
	const details = page.getByTestId('media-details')
	await expect(details).toContainText(name)
	await details.getByTestId('media-alt').fill('Library alt')
	await details.getByTestId('media-save').click()
	await expect(item).not.toContainText('No alt text')
	await expect(item.locator('img')).toHaveAttribute('alt', 'Library alt')

	// far-right sidebar on desktop; clicking the selected item again or × closes it
	const vw = page.viewportSize()!.width
	const box = (await details.boundingBox())!
	expect(Math.round(box.x + box.width)).toBe(vw)
	await item.click()
	await expect(details).toBeHidden()
	await item.click()
	await details.getByRole('button', { name: 'Close' }).click()
	await expect(details).toBeHidden()
	await item.click()

	await details.getByTestId('media-delete').click()
	await page.getByTestId('confirm-ok').click()
	await expect(item).toHaveCount(0)
})

test('published page renders the media alt, updated from the library', async ({ staffPage: page, browser }) => {
	await createPage(page)
	const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`
	await page.getByTestId('page-slug').fill(slug)
	await page.getByTestId('page-slug').press('Enter')

	await insertBlock(page, 'Image')
	const name = await uploadImage(page.locator('main'))
	await expect(page.locator('main figure img')).toBeVisible()
	await page.getByTestId('publish-toggle').click()
	await expect(page.getByTestId('publish-toggle')).toHaveText('Unpublish')

	const visitor = await (await browser.newContext()).newPage()
	const response = await visitor.goto(`/${slug}`)
	// images only from our own origin
	expect(response!.headers()['content-security-policy']).toContain("img-src 'self';")
	const img = visitor.locator('figure img')
	await expect(img).toHaveAttribute('alt', 'image')
	// same-origin, publicly readable
	await expect.poll(() => img.evaluate((el: HTMLImageElement) => el.naturalWidth)).toBe(40)

	// alt lives on the media object: changing it in the library updates the published page
	await page.goto('/admin/media')
	await page.getByTestId('media-item').filter({ hasText: name }).click()
	await page.getByTestId('media-alt').fill('Updated in library')
	await page.getByTestId('media-save').click()
	await expect(page.getByTestId('media-save')).toBeDisabled()

	await visitor.reload()
	await expect(img).toHaveAttribute('alt', 'Updated in library')
})

test('media library: rename keeps the file URL', async ({ staffPage: page }) => {
	await page.goto('/admin/media')
	const name = await uploadImage(page.locator('body'))
	const details = page.getByTestId('media-details')
	const src = await details.locator('img').getAttribute('src')

	const renamed = `renamed-${crypto.randomUUID().slice(0, 8)}.png`
	await details.getByTestId('media-name').fill(renamed)
	await details.getByTestId('media-save').click()
	await expect(details.getByTestId('media-save')).toBeDisabled()
	await expect(page.getByTestId('media-item').filter({ hasText: renamed })).toHaveCount(1)
	await expect(page.getByTestId('media-item').filter({ hasText: name })).toHaveCount(0)
	await expect(details.locator('img')).toHaveAttribute('src', src!)

	// blank name can't be saved
	await details.getByTestId('media-name').fill('  ')
	await expect(details.getByTestId('media-save')).toBeDisabled()
})

test('media details open as a modal on phones', async ({ staffPage: page }) => {
	await page.setViewportSize({ width: 390, height: 800 })
	await page.goto('/admin/media')
	await uploadImage(page.locator('body'))
	const details = page.getByTestId('media-details')
	await expect(details).toBeVisible()
	// backdrop click dismisses
	await page.mouse.click(5, 5)
	await expect(details).toBeHidden()
})

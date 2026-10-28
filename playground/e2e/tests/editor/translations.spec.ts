import { test, expect, createPage, editLanguage, insertBlock, publish } from '../../fixtures/staff.fixture'

// the playground is localized (en-US default + de-CH, it-CH, fr-CH): one block tree, text per language
test('a German translation is published at /de/<slug>, untranslated languages show the default', async ({
	staffPage: page,
	browser,
}) => {
	await createPage(page, 'Translated page')
	const slug = `e2e-${crypto.randomUUID().slice(0, 8)}`
	await page.getByTestId('page-slug').fill(slug)
	await page.getByTestId('page-slug').press('Enter')
	await insertBlock(page, 'Paragraph')
	await page.getByPlaceholder('Click to add text...').fill('Hello world')
	await page.getByTestId('page-save').click()
	await publish(page)

	await editLanguage(page, 'Deutsch')
	await expect(page.getByTestId('translation-hint')).toBeVisible()
	// untranslated: empty, the default's text as placeholder
	const german = page.getByPlaceholder('Hello world')
	await german.fill('Hallo Welt')
	await page.getByTestId('page-title').fill('Übersetzte Seite')

	// undo reverts the translation only
	await german.blur()
	await page.keyboard.press('Control+z')
	await expect(german).toHaveValue('')
	await german.fill('Hallo Welt')
	await german.blur()

	await page.getByTestId('page-save').click()
	await page.getByTestId('sidebar-tab-page').click()
	await expect(page.getByTestId('page-status')).toHaveText('Published · unpublished changes')
	await publish(page)

	const visitor = await (await browser.newContext()).newPage()
	await expect
		.poll(async () => {
			await visitor.goto(`/de/${slug}`)
			return visitor.getByText('Hallo Welt').count()
		}, { timeout: 15_000 })
		.toBe(1)
	await expect(visitor.getByRole('heading', { name: 'Übersetzte Seite' })).toBeVisible()

	await visitor.goto(`/fr/${slug}`)
	await expect(visitor.getByText('Hello world')).toBeVisible()
	await expect(visitor.getByRole('heading', { name: 'Translated page' })).toBeVisible()
	await visitor.goto(`/${slug}`)
	await expect(visitor.getByText('Hello world')).toBeVisible()
})

test('changing the default text marks its translation outdated until it is edited', async ({ staffPage: page }) => {
	await createPage(page, 'Outdated page')
	await insertBlock(page, 'Paragraph')
	// the one paragraph on the canvas: its textarea shows the edited language's text
	const paragraph = page.locator('main textarea').first()
	await paragraph.fill('Hello world')
	await paragraph.blur()

	await editLanguage(page, 'Deutsch')
	await paragraph.fill('Hallo Welt')
	await paragraph.blur()
	await editLanguage(page, 'English')
	await paragraph.fill('Hello there')
	await paragraph.blur()

	await editLanguage(page, 'Deutsch')
	await expect(paragraph).toHaveValue('Hallo Welt')
	await expect(page.locator('[data-outdated]')).toHaveCount(1)
	// the untranslated title + the outdated paragraph: badge on the trigger (German is being edited)
	const trigger = page.getByTestId('edit-language').getByRole('button')
	await expect(trigger).toHaveText(/DE\s*2$/)
	await paragraph.fill('Hallo zusammen')
	await expect(page.locator('[data-outdated]')).toHaveCount(0)
	await expect(trigger).toHaveText(/DE\s*1$/)
})

import { test, expect, createPage, insertBlock, publish } from '../../fixtures/staff.fixture'

// the playground is localized (en-US default + de-CH, fr-CH, it-CH): one block tree, text per language
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

	await page.getByTestId('edit-language').selectOption('de')
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

test('preview: the footer language switch changes the previewed language, not the admin', async ({
	staffPage: page,
	context,
}) => {
	await createPage(page, 'Preview page')
	await page.getByTestId('edit-language').selectOption('de')
	await page.getByTestId('page-title').fill('Vorschauseite')
	await page.getByTestId('page-title').blur() // saves
	await expect(page.getByTestId('page-save')).toBeDisabled()

	await page.goto(`${new URL(page.url()).pathname}/preview?lang=de`)
	await expect(page.getByRole('heading', { name: 'Vorschauseite' })).toBeVisible()
	const footer = page.getByTestId('site-footer')
	await footer.getByTestId('language-dropdown').getByRole('button', { name: 'Deutsch' }).click()
	await page.getByRole('menuitem', { name: 'Français' }).click()

	await expect(page).toHaveURL(/[?&]lang=fr$/)
	await expect(page.getByRole('heading', { name: 'Preview page' })).toBeVisible()
	await expect(page.getByTestId('preview-language')).toHaveValue('fr')
	expect((await context.cookies()).find((c) => c.name === 'cms_lang')).toBeUndefined()
})

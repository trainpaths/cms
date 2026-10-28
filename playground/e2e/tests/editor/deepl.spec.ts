import { test, expect, createPage, editLanguage, insertBlock } from '../../fixtures/staff.fixture'

// DeepL is stubbed: the instance endpoint says a key is set, /api/translate upper-cases. CI needs no key.
test('"Translate missing fields" fills the edited language in one undo step', async ({ staffPage: page }) => {
	await page.route('**/api/public/instance', async (route) => {
		const response = await route.fetch()
		await route.fulfill({ response, json: { ...(await response.json()), machineTranslation: true } })
	})
	await page.route('**/api/translate', async (route) => {
		const { texts } = route.request().postDataJSON() as { texts: string[] }
		await route.fulfill({ json: { texts: texts.map((t) => t.toUpperCase()) } })
	})
	// the instance config loads once, at admin start
	await page.reload()

	await createPage(page, 'Machine page')
	await insertBlock(page, 'Paragraph')
	await page.getByPlaceholder('Click to add text...').fill('Hello world')
	await page.getByPlaceholder('Click to add text...').blur()

	await editLanguage(page, 'Deutsch')
	// the inserted block switched the sidebar to its Block tab
	await page.getByTestId('sidebar-tab-page').click()
	await page.getByTestId('deepl-missing').click()
	await expect(page.getByText('fields translated by DeepL, please review')).toBeVisible()
	const paragraph = page.getByPlaceholder('Hello world')
	await expect(paragraph).toHaveValue('HELLO WORLD')
	await expect(page.getByTestId('page-title')).toHaveValue('MACHINE PAGE')

	await page.keyboard.press('Control+z')
	await expect(paragraph).toHaveValue('')
})

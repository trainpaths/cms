import { test, expect, createPage, insertBlock } from '../../fixtures/staff.fixture'

test('left sidebar outline lists blocks and child blocks; clicking selects', async ({ staffPage: page }) => {
	await createPage(page)
	await insertBlock(page, 'Heading')
	await page.getByPlaceholder('Click to add heading...').fill('Outline heading')
	await insertBlock(page, 'Card')
	await page.locator('main').getByRole('button', { name: '+ Link' }).click()

	await page.getByTestId('sidebar-tab-outline').click()
	await expect(page.getByTestId('sidebar-tab-outline')).toHaveAttribute('aria-selected', 'true')
	const items = page.getByTestId('outline-item')
	await expect(items).toHaveCount(3)
	await expect(items.nth(0)).toContainText('Heading')
	await expect(items.nth(0)).toContainText('Outline heading')
	await expect(items.nth(1)).toContainText('Card')
	await expect(items.nth(2)).toContainText('Link')

	// link is nested one level deeper than the card
	const cardX = (await items.nth(1).boundingBox())!.x
	const linkX = (await items.nth(2).boundingBox())!.x
	expect(linkX).toBeGreaterThan(cardX)

	await items.nth(0).click()
	await expect(items.nth(0)).toHaveAttribute('aria-current', 'true')

	// collapse hides the children
	await page.getByRole('button', { name: 'Collapse' }).click()
	await expect(items).toHaveCount(2)

	// back to the inserter
	await page.getByTestId('sidebar-tab-blocks').click()
	await expect(page.getByTestId('inserter-block').first()).toBeVisible()
})

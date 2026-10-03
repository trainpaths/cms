import { test, expect, createPage } from '../../fixtures/staff.fixture'
import { test as anonTest } from '@playwright/test'

test.describe('Pages admin', () => {
	test('lists pages and creates a new one', async ({ staffPage: page }) => {
		const title = await createPage(page)
		await expect(page.getByTestId('page-title')).toHaveValue(title)
		await expect(page.getByText('Start adding blocks from the sidebar')).toBeVisible()

		await page.getByRole('button', { name: 'Back', exact: true }).click()
		await expect(page).toHaveURL(/\/admin\/pages$/)
		await expect(page.getByTestId('page-row').filter({ hasText: title })).toBeVisible()
	})

	test('nav links to pages for staff', async ({ staffPage: page }) => {
		await page.getByRole('navigation').getByRole('link', { name: 'Pages' }).click()
		await expect(page.getByRole('heading', { name: 'Pages', exact: true })).toBeVisible()
	})

	test('can delete a page', async ({ staffPage: page }) => {
		const title = await createPage(page)
		await page.getByRole('button', { name: 'Back', exact: true }).click()
		await page.getByTestId('page-row').filter({ hasText: title }).getByTitle('Delete page').click()
		await page.getByTestId('confirm-ok').click()
		await expect(page.getByTestId('page-row').filter({ hasText: title })).toHaveCount(0)
	})
})

anonTest.describe('Pages admin access', () => {
	anonTest('redirects anonymous users to staff login', async ({ page }) => {
		await page.goto('/admin/pages')
		await expect(page).toHaveURL('/admin/login')
	})
})

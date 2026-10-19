import { test, expect } from '@playwright/test'
import { uniqueEmail, registerCustomer, loginCustomer } from '../../fixtures/test-utils'

test.describe('Update Profile', () => {
	test('can update display name', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('update-profile'), { displayName: 'Original Name' })

		await page.goto('/user/profile')
		await expect(page.getByText('Original Name')).toBeVisible()

		await page.getByRole('button', { name: 'Edit' }).click()
		await page.getByRole('textbox').fill('Updated Name')
		await page.getByRole('button', { name: 'Save' }).click()

		await expect(page.getByText('Profile updated successfully')).toBeVisible()
		await expect(page.getByText('Updated Name')).toBeVisible()
	})

	test('cancel edit reverts to original value', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('update-cancel'), { displayName: 'Original Name' })

		await page.goto('/user/profile')
		await page.getByRole('button', { name: 'Edit' }).click()
		await page.getByRole('textbox').fill('Changed Name')
		await page.getByRole('button', { name: 'Cancel' }).click()

		await expect(page.getByText('Original Name')).toBeVisible()
		await expect(page.getByText('Changed Name')).not.toBeVisible()
	})

	test('updated display name persists after logout and login', async ({ page }) => {
		const email = uniqueEmail('update-persist')
		await registerCustomer(page, email)

		await page.goto('/user/profile')
		await page.getByRole('button', { name: 'Edit' }).click()
		await page.getByRole('textbox').fill('Persisted Name')
		await page.getByRole('button', { name: 'Save' }).click()
		await expect(page.getByText('Profile updated successfully')).toBeVisible()

		await page.getByRole('button', { name: 'Logout' }).click()
		await expect(page).toHaveURL('/')

		await loginCustomer(page, email, undefined, '/user/profile')
		await expect(page.getByText('Persisted Name')).toBeVisible()
	})
})

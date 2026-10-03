import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from '../../fixtures/test-utils'

test.describe('Update Profile', () => {
	test('can update display name', async ({ page }) => {
		const email = uniqueEmail('update-profile')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByPlaceholder('Display name (optional)').fill('Original Name')
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await expect(page.getByText('Original Name')).toBeVisible()

		await page.getByRole('button', { name: 'Edit' }).click()
		await page.getByRole('textbox').fill('Updated Name')
		await page.getByRole('button', { name: 'Save' }).click()

		await expect(page.getByText('Profile updated successfully')).toBeVisible()
		await expect(page.getByText('Updated Name')).toBeVisible()
	})

	test('cancel edit reverts to original value', async ({ page }) => {
		const email = uniqueEmail('update-cancel')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByPlaceholder('Display name (optional)').fill('Original Name')
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await page.getByRole('button', { name: 'Edit' }).click()
		await page.getByRole('textbox').fill('Changed Name')
		await page.getByRole('button', { name: 'Cancel' }).click()

		await expect(page.getByText('Original Name')).toBeVisible()
		await expect(page.getByText('Changed Name')).not.toBeVisible()
	})

	test('updated display name persists after logout and login', async ({ page }) => {
		const email = uniqueEmail('update-persist')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await expect(page.getByRole('button', { name: 'Edit' })).toBeVisible()
		await page.getByRole('button', { name: 'Edit' }).click()
		await page.getByRole('textbox').fill('Persisted Name')
		await page.getByRole('button', { name: 'Save' }).click()
		await expect(page.getByText('Profile updated successfully')).toBeVisible()

		await page.getByRole('button', { name: 'Logout' }).click()
		await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible()

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /^Log in$/ }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await expect(page.getByRole('heading', { name: 'Profile' })).toBeVisible()
		await expect(page.getByText('Persisted Name')).toBeVisible()
	})
})

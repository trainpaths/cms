import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from '../../fixtures/test-utils'

test.describe('Reset Password', () => {
	test('missing token shows an invalid-link error', async ({ page }) => {
		await page.goto('/reset-password')

		await expect(page.getByText(/This reset link is invalid or incomplete/i)).toBeVisible()
		await expect(page.getByRole('link', { name: 'forgot password' })).toBeVisible()
	})

	test('shows the form when a token is present', async ({ page }) => {
		await page.goto('/reset-password?token=some-token')

		await expect(page.getByPlaceholder('New password (min 8 characters)')).toBeVisible()
		await expect(page.getByPlaceholder('Confirm new password')).toBeVisible()
		await expect(page.getByText(/This reset link is invalid or incomplete/i)).toBeHidden()
	})

	test('mismatched passwords disable submit and show an error', async ({ page }) => {
		await page.goto('/reset-password?token=some-token')

		await page.getByPlaceholder('New password (min 8 characters)').fill('NewPassword456!')
		await page.getByPlaceholder('Confirm new password').fill('DifferentPassword789!')

		await expect(page.getByText('Passwords do not match')).toBeVisible()
		await expect(page.getByRole('button', { name: 'Reset password' })).toBeDisabled()
	})

	test('invalid token is rejected by the API', async ({ page }) => {
		await page.goto('/reset-password?token=invalid-token')

		const newPassword = 'NewPassword456!'
		await page.getByPlaceholder('New password (min 8 characters)').fill(newPassword)
		await page.getByPlaceholder('Confirm new password').fill(newPassword)
		await page.getByRole('button', { name: 'Reset password' }).click()

		await expect(page.getByText(/Invalid or expired token/i)).toBeVisible()
		await expect(page).toHaveURL(/\/reset-password/)
	})

	test('reachable while authenticated (public route)', async ({ page }) => {
		const email = uniqueEmail('reset-public')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/reset-password?token=some-token')
		await expect(page).toHaveURL(/\/reset-password/)
		await expect(page.getByPlaceholder('New password (min 8 characters)')).toBeVisible()
	})
})

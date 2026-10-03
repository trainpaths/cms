import { test, expect } from '@playwright/test'
import { uniqueEmail } from '../../fixtures/test-utils'

test.describe('Verify Email', () => {
	test('missing token shows the error state and resend form', async ({ page }) => {
		await page.goto('/verify-email')

		await expect(page.getByText(/This verification link is invalid or has expired/i)).toBeVisible()
		await expect(page.getByPlaceholder('Email to resend verification to')).toBeVisible()
	})

	test('invalid token shows the error state after confirming on mount', async ({ page }) => {
		await page.goto('/verify-email?token=invalid-token')

		await expect(page.getByText(/This verification link is invalid or has expired/i)).toBeVisible()
		await expect(page.getByPlaceholder('Email to resend verification to')).toBeVisible()
	})

	test('resending verification shows a neutral confirmation', async ({ page }) => {
		await page.goto('/verify-email')

		await page.getByPlaceholder('Email to resend verification to').fill(uniqueEmail('verify-resend'))
		await page.getByRole('button', { name: 'Resend verification' }).click()

		await expect(page.getByText(/a new verification link has been sent/i)).toBeVisible()
		await expect(page.getByPlaceholder('Email to resend verification to')).toBeHidden()
	})

	test('back-to-login link returns to login', async ({ page }) => {
		await page.goto('/verify-email')
		await page.getByRole('link', { name: 'Back to login' }).click()
		await expect(page).toHaveURL('/login')
	})
})

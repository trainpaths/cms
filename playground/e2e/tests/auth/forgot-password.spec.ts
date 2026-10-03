import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from '../../fixtures/test-utils'

test.describe('Forgot Password', () => {
	test('reachable from the login page', async ({ page }) => {
		await page.goto('/login')
		await page.getByRole('link', { name: 'Forgot password?' }).click()

		await expect(page).toHaveURL('/forgot-password')
		await expect(page.getByRole('heading', { name: 'Reset your password' })).toBeVisible()
	})

	test('shows neutral confirmation for a registered email', async ({ page }) => {
		const email = uniqueEmail('forgot-registered')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await page.getByRole('button', { name: 'Logout' }).click()

		await page.goto('/forgot-password')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByRole('button', { name: 'Send reset link' }).click()

		await expect(page.getByText(/a password reset link has been sent/i)).toBeVisible()
		await expect(page.getByPlaceholder('Email')).toBeHidden()
	})

	test('shows the same confirmation for an unknown email (no enumeration)', async ({ page }) => {
		await page.goto('/forgot-password')
		await page.getByPlaceholder('Email').fill(uniqueEmail('forgot-unknown'))
		await page.getByRole('button', { name: 'Send reset link' }).click()

		await expect(page.getByText(/a password reset link has been sent/i)).toBeVisible()
		await expect(page.getByPlaceholder('Email')).toBeHidden()
	})

	test('authenticated user is redirected away (guest-only route)', async ({ page }) => {
		const email = uniqueEmail('forgot-guest')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/forgot-password')
		await expect(page).toHaveURL('/admin')
	})

	test('back-to-login link returns to login', async ({ page }) => {
		await page.goto('/forgot-password')
		await page.getByRole('link', { name: 'Back to login' }).click()
		await expect(page).toHaveURL('/login')
	})
})

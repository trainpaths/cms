import { test, expect } from '@playwright/test'
import { uniqueEmail, testPassword } from '../../fixtures/test-utils'

test.describe('Auth Routing', () => {
	test('unauthenticated user redirected from protected route to login', async ({ page }) => {
		await page.goto('/profile')
		await expect(page).toHaveURL('/login')
	})

	test('unauthenticated user redirected from admin area to staff login', async ({ page }) => {
		await page.goto('/admin')
		await expect(page).toHaveURL('/admin/login')
	})

	test('unauthenticated user redirected from change-password to login', async ({ page }) => {
		await page.goto('/change-password')
		await expect(page).toHaveURL('/login')
	})

	test('authenticated user redirected from login to dashboard', async ({ page }) => {
		const email = uniqueEmail('auth-redirect')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/login')
		await expect(page).toHaveURL('/admin')
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
	})

	test('authenticated user redirected from register to dashboard', async ({ page }) => {
		const email = uniqueEmail('auth-redirect-reg')

		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/register')
		await expect(page).toHaveURL('/admin')
	})

	test('redirect after login preserves intended destination', async ({ page }) => {
		await page.goto('/profile')
		await expect(page).toHaveURL('/login')

		const email = uniqueEmail('redirect-dest')
		await page.goto('/register')
		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

		await page.goto('/profile')
		await page.getByRole('button', { name: 'Logout' }).click()

		await page.goto('/change-password')
		await expect(page).toHaveURL('/login')

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill(testPassword())
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page).toHaveURL('/change-password')
	})
})

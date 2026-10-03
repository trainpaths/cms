import { test, expect } from '@playwright/test'

test.describe('Smoke Tests', () => {
	test('unauthenticated user redirects to login', async ({ page }) => {
		await page.goto('/profile')
		await expect(page).toHaveURL('/login')
		await expect(page.getByRole('heading', { name: 'Login' })).toBeVisible()
	})

	test('login page has register link', async ({ page }) => {
		await page.goto('/login')
		await expect(page.getByRole('link', { name: 'Register' })).toBeVisible()
	})

	test('register page has login link', async ({ page }) => {
		await page.goto('/register')
		await expect(page.getByRole('link', { name: 'Login' })).toBeVisible()
	})

	test('customer and staff logins are separate pages', async ({ page }) => {
		await page.goto('/login')
		await expect(page.getByRole('heading', { name: 'Login', exact: true })).toBeVisible()

		await page.goto('/admin/login')
		await expect(page.getByRole('heading', { name: 'Admin Login' })).toBeVisible()
	})

	test('404 page shows for unknown routes', async ({ page }) => {
		await page.goto('/unknown-route-xyz')
		await expect(page.getByRole('heading', { name: '404' })).toBeVisible()
	})
})

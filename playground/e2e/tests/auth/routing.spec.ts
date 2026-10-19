import { test, expect } from '@playwright/test'
import { uniqueEmail, registerCustomer, logoutCustomer } from '../../fixtures/test-utils'

test.describe('Auth Routing', () => {
	test('account pages send visitors to login, which comes back', async ({ page }) => {
		const email = uniqueEmail('redirect-dest')
		await registerCustomer(page, email)
		await logoutCustomer(page)

		await page.goto('/user/password')
		await expect(page).toHaveURL('/login?redirect=/user/password')

		await page.getByPlaceholder('Email').fill(email)
		await page.getByPlaceholder('Password').fill('TestPassword123!')
		await page.getByRole('button', { name: /^Log in$/ }).click()

		await expect(page).toHaveURL('/user/password')
		await expect(page.getByPlaceholder('Current password')).toBeVisible()
	})

	test('the redirect carries over to register', async ({ page }) => {
		await page.goto('/login?redirect=/legal')
		await page.getByRole('link', { name: 'Register' }).click()
		await expect(page).toHaveURL('/register?redirect=%2Flegal')
		await page.getByPlaceholder('Email').fill(uniqueEmail('redirect-register'))
		await page.getByPlaceholder('Password').fill('TestPassword123!')
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page).toHaveURL('/legal')
	})

	test('unsafe redirects go home instead', async ({ page }) => {
		await page.goto('/register?redirect=//evil.test')
		await page.getByPlaceholder('Email').fill(uniqueEmail('redirect-unsafe'))
		await page.getByPlaceholder('Password').fill('TestPassword123!')
		await page.getByRole('button', { name: /Register as customer/i }).click()
		await expect(page).toHaveURL('/')
	})

	test('unauthenticated user redirected from admin area to staff login', async ({ page }) => {
		await page.goto('/admin')
		await expect(page).toHaveURL('/admin/login')
	})

	test('signed-in customer opening login or register goes home', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('auth-redirect'))

		await page.goto('/login')
		await expect(page).toHaveURL('/')
		await expect(page.getByRole('heading', { name: 'Home', level: 1 })).toBeVisible()

		await page.goto('/register')
		await expect(page).toHaveURL('/')
	})

	test('customers have no access to the admin', async ({ page }) => {
		await registerCustomer(page, uniqueEmail('no-admin'))

		for (const path of ['/admin', '/admin/pages', '/admin/profile']) {
			await page.goto(path)
			await expect(page).toHaveURL('/')
			await expect(page.getByRole('heading', { name: 'Home', level: 1 })).toBeVisible()
		}
	})
})

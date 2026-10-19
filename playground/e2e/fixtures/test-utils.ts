import { expect, type Page } from '@playwright/test'

export function uniqueEmail(prefix: string = 'e2e'): string {
    return `${prefix}-${crypto.randomUUID()}@test.local`
}

export function testPassword(): string {
    return 'TestPassword123!'
}

export const REFRESH_COOKIE = 'refresh_token'

/** Signs up a fresh customer on /register (public site); without `?redirect` the screen returns home. */
export async function registerCustomer(page: Page, email: string, options: { password?: string; displayName?: string } = {}) {
    await page.goto('/register')
    await page.getByPlaceholder('Email').fill(email)
    await page.getByPlaceholder('Password').fill(options.password ?? testPassword())
    if (options.displayName) await page.getByPlaceholder('Display name (optional)').fill(options.displayName)
    await page.getByRole('button', { name: /Register as customer/i }).click()
    await expect(page).toHaveURL('/')
}

/** Logs in on /login (`redirect` = the page to come back to); waits until it got there. */
export async function loginCustomer(page: Page, email: string, password = testPassword(), redirect = '/') {
    await page.goto(redirect === '/' ? '/login' : `/login?redirect=${encodeURIComponent(redirect)}`)
    await page.getByPlaceholder('Email').fill(email)
    await page.getByPlaceholder('Password').fill(password)
    await page.getByRole('button', { name: /^Log in$/ }).click()
    await expect(page).toHaveURL(redirect)
}

/** Logs the customer out from their account page, which goes home. */
export async function logoutCustomer(page: Page) {
    await page.goto('/user/profile')
    await page.getByRole('button', { name: 'Logout' }).click()
    await expect(page).toHaveURL('/')
}

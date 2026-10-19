import { describe, expect, it } from 'vitest'
import { safeRedirect } from './redirect'

describe('safeRedirect', () => {
	it('keeps same-site paths with query and hash', () => {
		expect(safeRedirect('/')).toBe('/')
		expect(safeRedirect('/about?x=1#team')).toBe('/about?x=1#team')
		expect(safeRedirect('/user/profile')).toBe('/user/profile')
	})

	it('rejects other sites and anything a browser could read as one', () => {
		for (const value of ['https://evil.test', '//evil.test', '/\\evil.test', '/\t/evil.test', 'about', '', null, ['/a']])
			expect(safeRedirect(value)).toBeNull()
	})

	it('rejects guest screens (loop) and the admin', () => {
		for (const value of ['/login', '/register?redirect=/x', '/forgot-password', '/admin', '/admin/pages'])
			expect(safeRedirect(value)).toBeNull()
		expect(safeRedirect('/administration')).toBe('/administration')
	})
})

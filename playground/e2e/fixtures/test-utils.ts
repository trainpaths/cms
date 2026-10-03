export function uniqueEmail(prefix: string = 'e2e'): string {
    return `${prefix}-${crypto.randomUUID()}@test.local`
}

export function testPassword(): string {
    return 'TestPassword123!'
}

export const REFRESH_COOKIE = 'refresh_token'

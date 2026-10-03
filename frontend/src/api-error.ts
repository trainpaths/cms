/**
 * Every failed SDK call throws this (the generated client runs with `throwOnError`, and the error
 * interceptor in `interceptors.ts` converts whatever the fetch client throws). `status` is 0 for
 * network errors.
 */
export class ApiError extends Error {
	readonly status: number

	constructor(message: string, status: number) {
		super(message)
		this.name = 'ApiError'
		this.status = status
	}
}

/** Message of an ApiError, else the fallback (e.g. for unexpected runtime errors). */
export function errorMessage(err: unknown, fallback: string): string {
	return err instanceof ApiError ? err.message : fallback
}

/**
 * Every failed SDK call throws this (the generated client runs with `throwOnError`, and the error
 * interceptor `toApiError` converts whatever the fetch client throws; registered by both apps). `status` is 0 for
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

/** Error interceptor: ProblemDetails (JSON), plain text, or a network TypeError (no response) → ApiError. */
export function toApiError(error: unknown, response: Response | undefined): ApiError {
	if (error instanceof ApiError) return error
	const status = response?.status ?? 0
	if (status === 413) return new ApiError('File exceeds 10 MB.', 413)
	const problem = typeof error === 'object' && error ? (error as { detail?: string; title?: string }) : {}
	const message = problem.detail ?? problem.title ?? (status ? `Request failed (${status})` : 'Network error')
	return new ApiError(message, status)
}

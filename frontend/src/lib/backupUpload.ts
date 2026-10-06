import type { BackupInfo } from '../api/types.gen'
import { ApiError } from '../api-error'
import { useAuthStore } from '../stores/auth'

/**
 * Uploads a backup archive with progress (fetch can't report upload progress, so XHR instead of the generated
 * client). Archives can be gigabytes: nginx and the API stream them to disk.
 */
export async function uploadBackup(file: File, onProgress: (fraction: number) => void): Promise<BackupInfo> {
	const auth = useAuthStore()
	if (!(await auth.ensureSession())) throw new ApiError('Session expired, log in again.', 401)

	const form = new FormData()
	form.append('file', file)
	return new Promise((resolve, reject) => {
		const xhr = new XMLHttpRequest()
		xhr.open('POST', '/api/backups/upload')
		xhr.setRequestHeader('Authorization', `Bearer ${auth.accessToken}`)
		xhr.responseType = 'json'
		xhr.upload.onprogress = (e) => {
			if (e.lengthComputable) onProgress(e.loaded / e.total)
		}
		xhr.onload = () => {
			if (xhr.status >= 200 && xhr.status < 300) return resolve(xhr.response as BackupInfo)
			const problem = (xhr.response ?? {}) as { detail?: string; title?: string }
			const message =
				xhr.status === 413
					? 'The file is larger than the server accepts.'
					: (problem.detail ?? problem.title ?? `Upload failed (${xhr.status})`)
			reject(new ApiError(message, xhr.status))
		}
		xhr.onerror = () => reject(new ApiError('Network error', 0))
		xhr.send(form)
	})
}

export function formatBytes(bytes: number): string {
	if (bytes < 1024 * 1024) return `${Math.max(1, Math.round(bytes / 1024))} KB`
	if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
	return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`
}

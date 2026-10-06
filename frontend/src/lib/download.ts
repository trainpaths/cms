/** Hands a blob to the browser as a file download. */
export function saveBlob(blob: Blob, fileName: string): void {
	const url = URL.createObjectURL(blob)
	const a = document.createElement('a')
	a.href = url
	a.download = fileName
	document.body.appendChild(a)
	a.click()
	a.remove()
	// revoking synchronously can cancel the download in some browsers
	setTimeout(() => URL.revokeObjectURL(url), 0)
}

export function saveJson(data: unknown, fileName: string): void {
	saveBlob(new Blob([JSON.stringify(data, null, '\t')], { type: 'application/json' }), fileName)
}

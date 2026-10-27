<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { DEFAULT_LANGUAGE, useFormat, useLanguageSwitch } from '../../i18n'
import {
	Alert,
	Badge,
	Button,
	Card,
	EmptyState,
	FormField,
	Icon,
	Input,
	Loading,
	ProgressBar,
	Select,
	useConfirm,
	useToast,
} from '@trainpaths/nb-ui'
import {
	deleteApiBackupsByName,
	getApiBackups,
	getApiBackupsByName,
	getApiBackupsSettings,
	postApiBackups,
	postApiBackupsByNameRestore,
	putApiBackupsSettings,
} from '../../api/sdk.gen'
import type { BackupInfo, BackupInterval, BackupKind, BackupSettingsResponse } from '../../api/types.gen'
import { errorMessage } from '../../api-error'
import { saveBlob } from '../../lib/download'
import { formatBytes, uploadBackup } from '../../lib/backupUpload'
import { backupIntervals, weekdayName } from '../../lib/backupSchedule'
import { useAuthStore } from '../../stores/auth'

/** Full site backups (super admins): schedule, storage info, back up now, upload, download, restore, delete. */

const router = useRouter()
const { t } = useI18n()
const format = useFormat()
const languageSwitch = useLanguageSwitch()
const tag = computed(() => languageSwitch?.current.value.tag ?? DEFAULT_LANGUAGE.tag)
const intervals = computed(() => backupIntervals(t))
const auth = useAuthStore()
const { confirm } = useConfirm()
const { toast } = useToast()

const settings = ref<BackupSettingsResponse | null>(null)
const backups = ref<BackupInfo[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const form = ref({ interval: 'off' as BackupInterval, timeOfDay: '03:00', weekday: 0, dayOfMonth: 1, retention: 7 })
const saving = ref(false)
const creating = ref(false)
const uploadProgress = ref<number | null>(null)
const restoring = ref<string | null>(null)
const uploadInput = ref<HTMLInputElement | null>(null)

// 1-28: every month has the day
const monthDays = Array.from({ length: 28 }, (_, i) => ({ value: i + 1, label: `${i + 1}.` }))
const weekdays = computed(() => Array.from({ length: 7 }, (_, value) => ({ value, label: weekdayName(value, tag.value) })))
const kindLabels: Record<BackupKind, string> = {
	auto: 'admin.backups.kinds.auto',
	manual: 'admin.backups.kinds.manual',
	'pre-restore': 'admin.backups.kinds.preRestore',
	upload: 'admin.backups.kinds.upload',
}
const kindColors: Record<BackupKind, string> = {
	auto: 'accent',
	manual: 'primary',
	'pre-restore': 'warning',
	upload: 'secondary',
}

const busy = computed(() => creating.value || uploadProgress.value !== null || restoring.value !== null)
const dirty = computed(() => {
	const s = settings.value
	if (!s) return false
	const f = form.value
	return (
		f.interval !== s.interval ||
		f.timeOfDay !== s.timeOfDay ||
		f.weekday !== s.weekday ||
		f.dayOfMonth !== s.dayOfMonth ||
		f.retention !== s.retention
	)
})

// schedule is UTC: show what that is here
const localTime = computed(() => {
	const [h, m] = form.value.timeOfDay.split(':').map(Number)
	if (h === undefined || m === undefined || Number.isNaN(h) || Number.isNaN(m)) return ''
	const d = new Date()
	d.setUTCHours(h, m, 0, 0)
	return format.time(d, { hour: '2-digit', minute: '2-digit' })
})

onMounted(load)

async function load() {
	loading.value = true
	error.value = null
	try {
		const [s, list] = await Promise.all([getApiBackupsSettings(), getApiBackups()])
		applySettings(s.data)
		backups.value = list.data
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.loadFailed'))
	} finally {
		loading.value = false
	}
}

function applySettings(s: BackupSettingsResponse) {
	settings.value = s
	form.value = {
		interval: s.interval,
		timeOfDay: s.timeOfDay,
		weekday: s.weekday,
		dayOfMonth: s.dayOfMonth,
		retention: s.retention,
	}
}

async function refreshList() {
	backups.value = (await getApiBackups()).data
}

async function saveSettings() {
	saving.value = true
	try {
		const { data } = await putApiBackupsSettings({ body: { ...form.value, retention: Number(form.value.retention) } })
		applySettings(data)
		toast({ message: t('admin.backups.scheduleSaved'), type: 'success' })
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.scheduleFailed'))
	} finally {
		saving.value = false
	}
}

async function createNow() {
	creating.value = true
	try {
		const { data } = await postApiBackups()
		backups.value = [data, ...backups.value]
		toast({ message: t('admin.backups.created', { name: data.name }), type: 'success' })
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.createFailed'))
	} finally {
		creating.value = false
	}
}

async function handleUpload(event: Event) {
	const input = event.target as HTMLInputElement
	const file = input.files?.[0]
	input.value = ''
	if (!file) return
	if (settings.value && file.size > settings.value.maxUploadBytes) {
		error.value = t('admin.backups.tooLarge', { size: formatBytes(settings.value.maxUploadBytes, tag.value) })
		return
	}
	uploadProgress.value = 0
	try {
		const info = await uploadBackup(file, (p) => (uploadProgress.value = p))
		await refreshList()
		toast({ message: t('admin.backups.uploaded', { name: info.name }), type: 'success' })
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.uploadFailed'))
	} finally {
		uploadProgress.value = null
	}
}

async function download(item: BackupInfo) {
	try {
		const { data } = await getApiBackupsByName({ path: { name: item.name }, parseAs: 'blob' })
		saveBlob(data as Blob, item.name)
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.downloadFailed'))
	}
}

async function remove(item: BackupInfo) {
	const ok = await confirm({
		title: t('admin.backups.deleteTitle', { name: item.name }),
		message: t('admin.backups.deleteHint'),
		confirmText: t('common.delete'),
		cancelText: t('common.cancel'),
		danger: true,
	})
	if (!ok) return
	try {
		await deleteApiBackupsByName({ path: { name: item.name } })
		backups.value = backups.value.filter((b) => b.name !== item.name)
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.deleteFailed'))
	}
}

async function restore(item: BackupInfo) {
	const ok = await confirm({
		title: t('admin.backups.restoreTitle', { name: item.name }),
		message: t('admin.backups.restoreHint', { date: formatDate(item.createdAt) }),
		confirmText: t('admin.backups.restore'),
		cancelText: t('common.cancel'),
		danger: true,
	})
	if (!ok) return
	restoring.value = item.name
	try {
		const { data } = await postApiBackupsByNameRestore({ path: { name: item.name } })
		toast({
			message: t('admin.backups.restored', { name: data.preRestoreBackup }),
			type: 'success',
			duration: 10000,
		})
		await auth.logout()
		await router.push({ name: 'admin-login' })
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.restoreFailed'))
		restoring.value = null
		await refreshList().catch(() => {})
	}
}

function formatDate(iso: string | null | undefined): string {
	return iso ? format.dateTime(iso) : '—'
}
</script>

<template>
	<div class="flex flex-col gap-16">
		<Alert
			v-if="error"
			type="error"
			dismissible
			data-testid="backups-error"
			@dismiss="error = null"
		>
			{{ error }}
		</Alert>

		<Loading
			v-if="loading && !settings"
			:label="$t('admin.backups.loading')"
		/>

		<template v-else-if="settings">
			<Alert
				v-if="restoring"
				type="warning"
			>
				{{ $t('admin.backups.restoring', { name: restoring }) }}
			</Alert>

			<Card :title="$t('admin.backups.automatic')">
				<form
					class="grid gap-12 sm:grid-cols-2"
					data-testid="backup-settings"
					@submit.prevent="saveSettings"
				>
					<FormField :label="$t('admin.backups.interval')">
						<Select
							v-model="form.interval"
							:options="intervals"
							data-testid="backup-interval"
						/>
					</FormField>
					<FormField
						v-if="form.interval === 'weekly' || form.interval === 'biweekly'"
						:label="$t('admin.backups.weekday')"
						:hint="form.interval === 'biweekly' ? $t('admin.backups.biweeklyHint') : undefined"
					>
						<Select
							v-model="form.weekday"
							:options="weekdays"
							data-testid="backup-weekday"
						/>
					</FormField>
					<FormField
						v-if="form.interval === 'monthly'"
						:label="$t('admin.backups.dayOfMonth')"
					>
						<Select
							v-model="form.dayOfMonth"
							:options="monthDays"
							data-testid="backup-day-of-month"
						/>
					</FormField>
					<FormField
						v-if="form.interval !== 'off'"
						:label="$t('admin.backups.time')"
						:hint="localTime ? $t('admin.backups.localTime', { time: localTime }) : undefined"
					>
						<input
							v-model="form.timeOfDay"
							type="time"
							required
							class="h-36 w-full rounded-sm border border-gray-300 bg-white px-12 text-sm focus:border-accent-dark focus:ring-1 focus:ring-accent-dark focus:outline-hidden"
							data-testid="backup-time"
						/>
					</FormField>
					<FormField
						v-if="form.interval !== 'off'"
						:label="$t('admin.backups.retention')"
						:hint="$t('admin.backups.retentionHint')"
					>
						<Input
							v-model.number="form.retention"
							type="number"
							min="1"
							max="100"
							data-testid="backup-retention"
						/>
					</FormField>
					<div class="flex items-end sm:col-span-2">
						<Button
							type="submit"
							bg="primary"
							:disabled="!dirty"
							:loading="saving"
							data-testid="backup-settings-save"
						>
							{{ $t('admin.backups.saveSchedule') }}
						</Button>
					</div>
				</form>
				<dl class="mt-16 mb-0 grid grid-cols-[auto_1fr] gap-x-16 gap-y-4 text-sm">
					<dt class="text-black/60">{{ $t('admin.backups.nextRun') }}</dt>
					<dd
						class="m-0"
						data-testid="backup-next-run"
					>
						{{ settings.nextRunAt ? formatDate(settings.nextRunAt) : $t('admin.backups.intervals.off') }}
					</dd>
					<dt class="text-black/60">{{ $t('admin.backups.lastRun') }}</dt>
					<dd class="m-0">{{ formatDate(settings.lastRunAt) }}</dd>
				</dl>
				<Alert
					v-if="settings.lastError"
					type="error"
					class="mt-12"
				>
					{{ $t('admin.backups.lastFailed', { error: settings.lastError }) }}
				</Alert>
			</Card>

			<Card :title="$t('admin.backups.storage')">
				<dl class="m-0 grid grid-cols-[auto_1fr] gap-x-16 gap-y-4 text-sm">
					<dt class="text-black/60">{{ $t('admin.backups.folder') }}</dt>
					<dd
						class="m-0"
						data-testid="backup-location"
					>
						<span
							v-if="settings.hostPath"
							class="font-mono break-all"
							>{{ settings.hostPath }}</span
						>
						<template v-else>{{ $t('admin.backups.noFolder') }}</template>
					</dd>
					<template v-if="settings.freeBytes != null">
						<dt class="text-black/60">{{ $t('admin.backups.freeSpace') }}</dt>
						<dd class="m-0">{{ formatBytes(settings.freeBytes, tag) }}</dd>
					</template>
				</dl>
				<p class="mt-12 mb-0 text-xs text-black/60">
					{{ $t('admin.backups.storageHint', { years: settings.maxAgeYears }) }}
				</p>
			</Card>

			<Card :title="$t('admin.backups.tab')">
				<div class="mb-12 flex flex-wrap gap-8">
					<Button
						bg="primary"
						:loading="creating"
						:disabled="busy && !creating"
						data-testid="backup-create"
						@click="createNow"
					>
						{{ $t('admin.backups.now') }}
					</Button>
					<Button
						variant="outline"
						text="primary"
						:disabled="busy"
						:title="$t('admin.backups.uploadHint')"
						data-testid="backup-upload"
						@click="uploadInput?.click()"
					>
						{{ $t('admin.backups.upload') }}
					</Button>
					<input
						ref="uploadInput"
						type="file"
						accept=".gz,application/gzip"
						class="hidden"
						data-testid="backup-upload-file"
						@change="handleUpload"
					/>
				</div>
				<ProgressBar
					v-if="uploadProgress !== null"
					:value="Math.round(uploadProgress * 100)"
					:label="$t('admin.backups.uploading')"
					class="mb-12"
				/>

				<EmptyState
					v-if="!backups.length"
					:title="$t('admin.backups.empty')"
					:description="$t('admin.backups.emptyHint')"
				/>
				<ul
					v-else
					class="m-0 flex list-none flex-col divide-y divide-gray-200 p-0"
				>
					<li
						v-for="item in backups"
						:key="item.name"
						class="flex flex-col gap-4 py-8 sm:flex-row sm:items-center sm:gap-8"
						data-testid="backup-row"
					>
						<div class="min-w-0 flex-1">
							<div class="flex flex-wrap items-center gap-8">
								<span class="text-sm font-medium">{{ formatDate(item.createdAt) }}</span>
								<Badge
									size="sm"
									:color="kindColors[item.kind]"
									data-testid="backup-kind"
									>{{ $t(kindLabels[item.kind]) }}</Badge
								>
							</div>
							<div class="mt-2 truncate text-xs text-black/60">
								<span class="font-mono">{{ item.name }}</span>
								· {{ formatBytes(item.size, tag) }}
								<template v-if="item.mediaCount != null"> · {{ $t('admin.backups.mediaFiles', item.mediaCount) }}</template>
								<template v-if="item.cmsVersion"> · CMS {{ item.cmsVersion }}</template>
							</div>
							<div
								v-if="item.error"
								class="mt-2 text-xs text-danger"
							>
								{{ item.error }}
							</div>
						</div>
						<div class="flex shrink-0 items-center gap-4">
							<Button
								bg="primary"
								size="sm"
								class="mr-4"
								:disabled="busy || !!item.error"
								:loading="restoring === item.name"
								data-testid="backup-restore"
								@click="restore(item)"
							>
								{{ $t('admin.backups.restore') }}
							</Button>
							<Button
								variant="ghost"
								text="secondary"
								square
								:title="$t('admin.backups.download')"
								:aria-label="$t('admin.backups.downloadName', { name: item.name })"
								data-testid="backup-download"
								@click="download(item)"
							>
								<Icon name="download" />
							</Button>
							<Button
								variant="ghost"
								text="danger"
								square
								:title="$t('common.delete')"
								:disabled="busy"
								:aria-label="$t('admin.backups.deleteName', { name: item.name })"
								data-testid="backup-delete"
								@click="remove(item)"
							>
								<Icon name="trash" />
							</Button>
						</div>
					</li>
				</ul>
			</Card>
		</template>
	</div>
</template>

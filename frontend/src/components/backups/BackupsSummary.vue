<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Alert, Card, Link, Loading } from '@trainpaths/nb-ui'
import { useI18n } from 'vue-i18n'
import { useFormat, useLanguageSwitch, DEFAULT_LANGUAGE } from '../../i18n'
import { getApiBackups, getApiBackupsSettings } from '../../api/sdk.gen'
import type { BackupInfo, BackupSettingsResponse } from '../../api/types.gen'
import { errorMessage } from '../../api-error'
import { formatBytes } from '../../lib/backupUpload'
import { scheduleSummary } from '../../lib/backupSchedule'

/** Dashboard card (super admins): backup schedule and what's stored, link to the Backups tab. */

const { t } = useI18n()
const format = useFormat()
const languageSwitch = useLanguageSwitch()
const tag = computed(() => languageSwitch?.current.value.tag ?? DEFAULT_LANGUAGE.tag)
const settings = ref<BackupSettingsResponse | null>(null)
const backups = ref<BackupInfo[]>([])
const error = ref<string | null>(null)

const totalSize = computed(() => backups.value.reduce((sum, b) => sum + b.size, 0))
// list is newest first
const latest = computed(() => backups.value[0] ?? null)
const autoCount = computed(() => backups.value.filter((b) => b.kind === 'auto').length)

const formatDate = (iso: string | null | undefined) => (iso ? format.dateTime(iso) : '—')

onMounted(async () => {
	try {
		const [s, list] = await Promise.all([getApiBackupsSettings(), getApiBackups()])
		settings.value = s.data
		backups.value = list.data
	} catch (err: unknown) {
		error.value = errorMessage(err, t('admin.backups.loadFailed'))
	}
})
</script>

<template>
	<Card data-testid="dashboard-backups">
		<template #header>
			<div class="flex items-center justify-between gap-16">
				<h3 class="m-0 text-base font-semibold text-black">{{ $t('admin.backups.tab') }}</h3>
				<Link
					to="/admin/profile?tab=backups"
					data-testid="dashboard-backups-link"
					>{{ $t('admin.backups.manage') }}</Link
				>
			</div>
		</template>
		<p
			v-if="error"
			class="m-0 text-sm text-danger"
		>
			{{ error }}
		</p>
		<Loading
			v-else-if="!settings"
			:label="$t('common.loading')"
		/>
		<template v-else>
			<dl class="m-0 grid grid-cols-[auto_1fr] gap-x-16 gap-y-4 text-sm">
				<dt class="text-black/60">{{ $t('admin.backups.schedule') }}</dt>
				<dd
					class="m-0"
					data-testid="dashboard-backups-schedule"
				>
					{{ scheduleSummary(settings, t, tag) }}
				</dd>
				<template v-if="settings.interval !== 'off'">
					<dt class="text-black/60">{{ $t('admin.backups.nextRun') }}</dt>
					<dd class="m-0">{{ formatDate(settings.nextRunAt) }}</dd>
					<dt class="text-black/60">{{ $t('admin.backups.lastRun') }}</dt>
					<dd class="m-0">{{ formatDate(settings.lastRunAt) }}</dd>
					<dt class="text-black/60">{{ $t('admin.backups.keeps') }}</dt>
					<dd class="m-0">{{ $t('admin.backups.keepsValue', { n: settings.retention }) }}</dd>
				</template>
				<dt class="text-black/60">{{ $t('admin.backups.saved') }}</dt>
				<dd
					class="m-0"
					data-testid="dashboard-backups-count"
				>
					{{ $t('admin.backups.count', backups.length) }}
					<span
						v-if="backups.length"
						class="text-black/60"
						>({{ $t('admin.backups.countDetail', { auto: autoCount, size: formatBytes(totalSize, tag) }) }})</span
					>
				</dd>
				<template v-if="latest">
					<dt class="text-black/60">{{ $t('admin.backups.latest') }}</dt>
					<dd class="m-0">{{ formatDate(latest.createdAt) }}</dd>
				</template>
				<template v-if="settings.freeBytes != null">
					<dt class="text-black/60">{{ $t('admin.backups.freeSpace') }}</dt>
					<dd class="m-0">{{ formatBytes(settings.freeBytes, tag) }}</dd>
				</template>
			</dl>
			<Alert
				v-if="settings.lastError"
				type="error"
				class="mt-12"
			>
				{{ $t('admin.backups.lastFailed', { error: settings.lastError }) }}
			</Alert>
		</template>
	</Card>
</template>

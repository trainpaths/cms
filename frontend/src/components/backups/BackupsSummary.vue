<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Alert, Card, Link, Loading } from '@trainpaths/nb-ui'
import { getApiBackups, getApiBackupsSettings } from '../../api/sdk.gen'
import type { BackupInfo, BackupSettingsResponse } from '../../api/types.gen'
import { errorMessage } from '../../api-error'
import { formatBytes } from '../../lib/backupUpload'
import { scheduleSummary } from '../../lib/backupSchedule'

/** Dashboard card (super admins): backup schedule and what's stored, link to the Backups tab. */

const settings = ref<BackupSettingsResponse | null>(null)
const backups = ref<BackupInfo[]>([])
const error = ref<string | null>(null)

const totalSize = computed(() => backups.value.reduce((sum, b) => sum + b.size, 0))
// list is newest first
const latest = computed(() => backups.value[0] ?? null)
const autoCount = computed(() => backups.value.filter((b) => b.kind === 'auto').length)

const formatDate = (iso: string | null | undefined) => (iso ? new Date(iso).toLocaleString() : '—')

onMounted(async () => {
	try {
		const [s, list] = await Promise.all([getApiBackupsSettings(), getApiBackups()])
		settings.value = s.data
		backups.value = list.data
	} catch (err: unknown) {
		error.value = errorMessage(err, 'Failed to load backups')
	}
})
</script>

<template>
	<Card data-testid="dashboard-backups">
		<template #header>
			<div class="flex items-center justify-between gap-16">
				<h3 class="m-0 text-base font-semibold text-black">Backups</h3>
				<Link
					to="/admin/profile?tab=backups"
					data-testid="dashboard-backups-link"
					>Manage backups</Link
				>
			</div>
		</template>
		<p
			v-if="error"
			class="m-0 text-sm text-danger"
		>
			{{ error }}
		</p>
		<Loading v-else-if="!settings" />
		<template v-else>
			<dl class="m-0 grid grid-cols-[auto_1fr] gap-x-16 gap-y-4 text-sm">
				<dt class="text-black/60">Schedule</dt>
				<dd
					class="m-0"
					data-testid="dashboard-backups-schedule"
				>
					{{ scheduleSummary(settings) }}
				</dd>
				<template v-if="settings.interval !== 'off'">
					<dt class="text-black/60">Next run</dt>
					<dd class="m-0">{{ formatDate(settings.nextRunAt) }}</dd>
					<dt class="text-black/60">Last run</dt>
					<dd class="m-0">{{ formatDate(settings.lastRunAt) }}</dd>
					<dt class="text-black/60">Keeps</dt>
					<dd class="m-0">last {{ settings.retention }} automatic</dd>
				</template>
				<dt class="text-black/60">Saved</dt>
				<dd
					class="m-0"
					data-testid="dashboard-backups-count"
				>
					{{ backups.length }} {{ backups.length === 1 ? 'backup' : 'backups' }}
					<span
						v-if="backups.length"
						class="text-black/60"
						>({{ autoCount }} automatic, {{ formatBytes(totalSize) }})</span
					>
				</dd>
				<template v-if="latest">
					<dt class="text-black/60">Latest</dt>
					<dd class="m-0">{{ formatDate(latest.createdAt) }}</dd>
				</template>
				<template v-if="settings.freeBytes != null">
					<dt class="text-black/60">Free space</dt>
					<dd class="m-0">{{ formatBytes(settings.freeBytes) }}</dd>
				</template>
			</dl>
			<Alert
				v-if="settings.lastError"
				type="error"
				class="mt-12"
			>
				Last automatic backup failed: {{ settings.lastError }}
			</Alert>
		</template>
	</Card>
</template>

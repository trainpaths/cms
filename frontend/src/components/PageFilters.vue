<script setup lang="ts">
import { computed } from 'vue'
import { Button, Input, Select } from '@trainpaths/nb-ui'
import type { PageSort, PageStatusFilter } from '../composables/usePageFilter'

/**
 * Search + tag + sort controls for page lists (pages admin, menu page picker). State: `usePageFilter`.
 * The status select shows only when `v-model:status` is bound (pages admin; the picker lists published pages only).
 */
const props = defineProps<{ tags: string[]; compact?: boolean }>()
const search = defineModel<string>('search', { required: true })
const tag = defineModel<string>('tag', { required: true })
const sort = defineModel<PageSort>('sort', { required: true })
const status = defineModel<PageStatusFilter>('status')

// '' = no tag filter, a real option (Select's placeholder is unselectable)
const tagOptions = computed(() => [{ value: '', label: 'All tags' }, ...props.tags])
const statusOptions: { value: PageStatusFilter; label: string }[] = [
	{ value: '', label: 'All statuses' },
	{ value: 'published', label: 'Published' },
	{ value: 'draft', label: 'Not published' },
]
</script>

<template>
	<div
		class="flex gap-8"
		:class="compact ? 'flex-col' : 'flex-wrap items-center'"
		data-testid="page-filters"
	>
		<Input
			v-model="search"
			type="search"
			class="min-w-0"
			:class="compact ? 'w-full' : 'min-w-160 flex-1'"
			placeholder="Search pages…"
			aria-label="Search pages"
			data-testid="page-filter-search"
		/>
		<div class="flex gap-8">
			<Select
				v-model="tag"
				:options="tagOptions"
				class="min-w-0 flex-1"
				:class="{ 'min-w-160': !compact }"
				aria-label="Filter by tag"
				data-testid="page-filter-tag"
			/>
			<Select
				v-if="status !== undefined"
				v-model="status"
				:options="statusOptions"
				class="min-w-0 flex-1"
				:class="{ 'min-w-160': !compact }"
				aria-label="Filter by status"
				data-testid="page-filter-status"
			/>
			<!-- sort by updatedAt: clock + arrow, down = newest first, up = oldest first -->
			<Button
				variant="outline"
				square
				class="shrink-0"
				:title="sort === 'updated-desc' ? 'Newest updated first' : 'Oldest updated first'"
				:aria-label="`Sorted by last update, ${sort === 'updated-desc' ? 'newest' : 'oldest'} first. Click to reverse.`"
				:data-sort="sort"
				data-testid="page-filter-sort"
				@click="sort = sort === 'updated-desc' ? 'updated-asc' : 'updated-desc'"
			>
				<svg
					xmlns="http://www.w3.org/2000/svg"
					width="20"
					height="20"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					stroke-linecap="round"
					stroke-linejoin="round"
					aria-hidden="true"
				>
					<circle
						cx="9"
						cy="12"
						r="7"
					/>
					<path d="M9 8v4l2.5 2" />
					<path
						v-if="sort === 'updated-desc'"
						d="M20 5v14m-3-3 3 3 3-3"
					/>
					<path
						v-else
						d="M20 19V5m-3 3 3-3 3 3"
					/>
				</svg>
			</Button>
		</div>
	</div>
</template>

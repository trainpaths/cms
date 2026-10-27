<script setup lang="ts" generic="T extends string">
import { CloseButton } from '@trainpaths/nb-ui'
/** Sidebar top row: tab bar + close X (both editor sidebars). Tab testids: `sidebar-tab-{id}`. */
defineProps<{
	/** label = message key */
	tabs: readonly { id: T; label: string }[]
	closeTestid: string
}>()

const active = defineModel<T>({ required: true })

const emit = defineEmits<{
	close: []
}>()
</script>

<template>
	<div class="flex shrink-0 border-b border-gray-200 px-8">
		<div
			class="flex flex-1"
			role="tablist"
		>
			<button
				v-for="tab in tabs"
				:key="tab.id"
				type="button"
				role="tab"
				:aria-selected="active === tab.id"
				class="-mb-px flex-1 cursor-pointer border-x-0 border-t-0 border-b-2 bg-transparent px-8 py-12 text-sm"
				:class="
					active === tab.id
						? 'border-primary font-medium text-primary'
						: 'border-transparent text-gray-500 hover:text-gray-800'
				"
				:data-testid="`sidebar-tab-${tab.id}`"
				@click="active = tab.id"
			>
				{{ $t(tab.label) }}
			</button>
		</div>
		<CloseButton
			class="mx-10 text-gray-500"
			:title="$t('common.close')"
			:label="$t('editor.closeSidebar')"
			:data-testid="closeTestid"
			@click="emit('close')"
		/>
	</div>
</template>

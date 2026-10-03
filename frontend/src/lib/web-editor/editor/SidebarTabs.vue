<script setup lang="ts" generic="T extends string">
/** Sidebar top row: tab bar + close X (both editor sidebars). Tab testids: `sidebar-tab-{id}`. */
defineProps<{
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
				{{ tab.label }}
			</button>
		</div>
		<button
			type="button"
			class="shrink-0 cursor-pointer border-none bg-transparent px-12 text-lg text-gray-500 hover:text-gray-800"
			title="Close"
			aria-label="Close sidebar"
			:data-testid="closeTestid"
			@click="emit('close')"
		>
			&#10005;
		</button>
	</div>
</template>

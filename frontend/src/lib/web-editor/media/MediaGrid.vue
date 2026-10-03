<script setup lang="ts">
import type { MediaItem } from '../core/types'

withDefaults(defineProps<{ items: MediaItem[]; selectedId?: string | null; gridClass?: string }>(), {
	selectedId: null,
	gridClass: 'grid-cols-[repeat(auto-fill,minmax(128px,1fr))]',
})
const emit = defineEmits<{ select: [item: MediaItem] }>()
</script>

<template>
	<ul
		class="m-0 grid list-none gap-12 p-0"
		:class="gridClass"
	>
		<li
			v-for="item in items"
			:key="item.id"
		>
			<button
				type="button"
				class="flex w-full cursor-pointer flex-col overflow-hidden rounded-md border bg-white p-0 text-left hover:border-primary"
				:class="item.id === selectedId ? 'border-primary ring-2 ring-primary' : 'border-gray-200'"
				:title="item.fileName"
				data-testid="media-item"
				@click="emit('select', item)"
			>
				<img
					:src="item.url"
					:alt="item.alt || item.fileName"
					loading="lazy"
					class="aspect-square w-full bg-gray-100 object-cover"
				/>
				<span class="truncate px-6 py-4 text-xs text-gray-600">{{ item.fileName }}</span>
				<span
					v-if="!item.alt"
					class="px-6 pb-4 text-[11px] text-yellow-700"
				>
					No alt text
				</span>
			</button>
		</li>
	</ul>
</template>

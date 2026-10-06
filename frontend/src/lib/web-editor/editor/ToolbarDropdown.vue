<script setup lang="ts" generic="T extends string | number">
import { ref, computed, watch, onBeforeUnmount } from 'vue'
import { Icon } from '@trainpaths/nb-ui'

/**
 * Compact inline-toolbar control: shows only the current option, click opens the choices.
 * The default slot renders an option (`value`) both in the button and in the menu.
 */
const props = defineProps<{
	options: { value: T; label: string }[]
	title: string
	/** also show each option's label next to it in the menu */
	showLabels?: boolean
}>()
const model = defineModel<T>({ required: true })

const open = ref(false)
const root = ref<HTMLElement | null>(null)
const currentLabel = computed(() => props.options.find((o) => o.value === model.value)?.label ?? '')

function choose(value: T) {
	model.value = value
	open.value = false
}

function onDocMouseDown(e: MouseEvent) {
	if (root.value && !root.value.contains(e.target as Node)) open.value = false
}

watch(open, (isOpen) => {
	if (isOpen) document.addEventListener('mousedown', onDocMouseDown)
	else document.removeEventListener('mousedown', onDocMouseDown)
})
onBeforeUnmount(() => document.removeEventListener('mousedown', onDocMouseDown))
</script>

<template>
	<div
		ref="root"
		class="relative"
	>
		<button
			class="dropdown-btn"
			:title="`${title}: ${currentLabel}`"
			aria-haspopup="listbox"
			:aria-expanded="open"
			@click="open = !open"
		>
			<slot :value="model" />
			<Icon name="chevron" :size="12" :rotate="open ? 90 : 270" />
		</button>
		<div
			v-if="open"
			role="listbox"
			class="absolute left-0 top-full z-30 mt-4 flex min-w-full flex-col gap-2 rounded-md bg-gray-900 p-4 shadow-lg"
		>
			<button
				v-for="opt in options"
				:key="opt.value"
				role="option"
				:aria-selected="opt.value === model"
				:title="opt.label"
				class="dropdown-btn"
				:class="opt.value === model ? 'bg-gray-700 text-white' : ''"
				@click="choose(opt.value)"
			>
				<slot :value="opt.value" />
				<span
					v-if="showLabels"
					class="whitespace-nowrap"
					>{{ opt.label }}</span
				>
			</button>
		</div>
	</div>
</template>

<style scoped>
@reference "../../../style.css";

.dropdown-btn {
	@apply flex cursor-pointer items-center gap-4 rounded border-none bg-transparent px-6 py-4 text-sm text-gray-300 hover:bg-gray-700 hover:text-white;
}
</style>

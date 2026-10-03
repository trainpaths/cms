<script setup lang="ts">
import SettingsSection from '../../editor/SettingsSection.vue'
import type { BlockInstance } from '../../core/types'
import { useBlockAttribute } from '../../composables/useBlockAttribute'

const props = defineProps<{ block: BlockInstance }>()

const ordered = useBlockAttribute(() => props.block, 'ordered', false)
const options = [
	{ value: false, label: 'Bullet' },
	{ value: true, label: 'Numbered' },
]
</script>

<template>
	<SettingsSection title="List type">
		<div class="flex rounded-md border border-gray-200">
			<button
				v-for="opt in options"
				:key="opt.label"
				class="flex-1 cursor-pointer border-none px-12 py-6 text-xs font-medium transition-colors"
				:class="ordered === opt.value ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'"
				@click="ordered = opt.value"
			>
				{{ opt.label }}
			</button>
		</div>
	</SettingsSection>
</template>

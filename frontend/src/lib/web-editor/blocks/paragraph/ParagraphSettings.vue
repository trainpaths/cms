<script setup lang="ts">
import SettingsSection from '../../editor/SettingsSection.vue'
import AlignIcon from './AlignIcon.vue'
import type { BlockInstance } from '../../core/types'
import { useBlockAttribute } from '../../composables/useBlockAttribute'
import { alignOptions } from './align'

const props = defineProps<{ block: BlockInstance }>()

const alignment = useBlockAttribute(() => props.block, 'alignment', 'left')
</script>

<template>
	<SettingsSection title="Alignment">
		<div class="flex rounded-md border border-gray-200">
			<button
				v-for="opt in alignOptions"
				:key="opt.value"
				class="flex flex-1 cursor-pointer justify-center border-none px-12 py-6 transition-colors"
				:class="alignment === opt.value ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'"
				:title="opt.label"
				@click="alignment = opt.value"
			>
				<AlignIcon :align="opt.value" />
			</button>
		</div>
	</SettingsSection>
</template>

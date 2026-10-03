<script setup lang="ts">
import SettingsSection from '../../editor/SettingsSection.vue'
import type { BlockInstance } from '../../core/types'
import { useBlockAttribute } from '../../composables/useBlockAttribute'

const props = defineProps<{ block: BlockInstance }>()

const level = useBlockAttribute(() => props.block, 'level', 2)
</script>

<template>
	<SettingsSection title="Level">
		<div class="flex rounded-md border border-gray-200">
			<button
				v-for="n in 6"
				:key="n"
				class="flex-1 cursor-pointer border-none px-4 py-6 text-xs font-medium transition-colors"
				:class="level === n ? 'bg-primary text-white' : 'bg-white text-gray-600 hover:bg-gray-50'"
				@click="level = n"
			>
				H{{ n }}
			</button>
		</div>
	</SettingsSection>
</template>

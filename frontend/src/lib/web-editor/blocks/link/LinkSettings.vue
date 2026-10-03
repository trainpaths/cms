<script setup lang="ts">
import SettingsSection from '../../editor/SettingsSection.vue'
import type { BlockInstance } from '../../core/types'
import { useBlockAttribute } from '../../composables/useBlockAttribute'

const props = defineProps<{ block: BlockInstance }>()

const url = useBlockAttribute(() => props.block, 'url', '')
</script>

<template>
	<SettingsSection title="Link">
		<label class="field-label">
			URL
			<input
				v-model="url"
				type="url"
				class="input-field"
				placeholder="https://..."
			/>
		</label>
	</SettingsSection>
</template>

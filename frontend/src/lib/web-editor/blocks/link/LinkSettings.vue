<script setup lang="ts">
import SettingsSection from '../../editor/SettingsSection.vue'
import type { BlockInstance } from '../../core/types'
import { useBlockAttribute } from '../../composables/useBlockAttribute'

const props = defineProps<{ block: BlockInstance }>()

const url = useBlockAttribute(() => props.block, 'url', '')
</script>

<template>
	<SettingsSection :title="$t('blocks.link.title')">
		<label class="field-label">
			{{ $t('editor.link.url') }}
			<input
				v-model="url"
				type="url"
				class="input-field"
				placeholder="https://..."
			/>
		</label>
	</SettingsSection>
</template>

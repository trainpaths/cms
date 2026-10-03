<script setup lang="ts">
import SettingsSection from '../../editor/SettingsSection.vue'
import MediaField from '../../media/MediaField.vue'
import type { BlockInstance } from '../../core/types'
import { useImageWidth } from './width'

const props = defineProps<{ block: BlockInstance }>()

const width = useImageWidth(() => props.block)
</script>

<template>
	<SettingsSection title="Image">
		<div class="flex flex-col gap-12">
			<MediaField :block="block" />
			<div class="field-label">
				Width (%)
				<div class="mt-2 flex items-center gap-8">
					<!-- commit on release: every input step would be an undo entry + autosave tick -->
					<input
						:value="width"
						type="range"
						min="1"
						max="100"
						class="flex-1 accent-primary"
						aria-label="Width slider"
						@change="width = Number(($event.target as HTMLInputElement).value)"
					/>
					<input
						:value="width"
						type="number"
						min="1"
						max="100"
						class="input-field mt-0 w-64"
						aria-label="Width"
						@change="width = Number(($event.target as HTMLInputElement).value)"
					/>
				</div>
			</div>
		</div>
	</SettingsSection>
</template>

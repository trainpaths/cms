<script setup lang="ts">
import SettingsSection from '../../editor/SettingsSection.vue'
import MediaField from '../../media/MediaField.vue'
import type { BlockInstance } from '../../core/types'

defineProps<{ block: BlockInstance }>()
</script>

<template>
	<SettingsSection :title="$t('blocks.image.title')">
		<MediaField :block="block" />
	</SettingsSection>
</template>

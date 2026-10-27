<script setup lang="ts">
import { Button } from '@trainpaths/nb-ui'
import { ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BlockInstance } from '../core/types'
import MediaActions from './MediaActions.vue'
import { useMediaImage } from './useMediaImage'
import { assignMedia } from './assignMedia'
import { useMediaStore } from '../../../stores/media'
import { useEditorStore } from '../../../stores/editor'
import { MEDIA_ALT_MAX } from '../limits'
import { errorMessage } from '../../../api-error'

/** Settings-panel image picker: preview, upload / choose / remove, and the media object's alt text. */
const props = defineProps<{ block: BlockInstance }>()

const media = useMediaStore()
const { t } = useI18n()
const editor = useEditorStore()
const { asset, src, missing } = useMediaImage(() => props.block)

// Alt belongs to the media object: edited locally, saved on change (not part of the page/undo history).
const alt = ref('')
watch(
	() => asset.value?.alt,
	(value) => (alt.value = value ?? ''),
	{ immediate: true },
)

async function saveAlt() {
	if (!asset.value || alt.value.trim() === asset.value.alt) return
	try {
		await media.update(asset.value.id, { alt: alt.value.trim() })
	} catch (err: unknown) {
		editor.errorMessage = errorMessage(err, t('editor.media.altFailed'))
	}
}
</script>

<template>
	<div class="flex flex-col gap-12">
		<img
			v-if="src"
			:src="src"
			alt=""
			class="max-h-160 w-full rounded border border-gray-200 bg-gray-50 object-contain"
		/>
		<p
			v-else-if="missing"
			class="m-0 text-xs text-red-600"
		>
			{{ $t('editor.media.deleted') }}
		</p>
		<MediaActions
			:replace="!!src"
			@select="assignMedia(block.id, $event.id)"
		/>
		<label
			v-if="asset"
			class="field-label"
		>
			{{ $t('admin.media.alt') }}
			<input
				v-model="alt"
				type="text"
				:maxlength="MEDIA_ALT_MAX"
				class="input-field"
				:placeholder="$t('admin.media.altPlaceholder')"
				data-testid="media-alt"
				@change="saveAlt"
			/>
		</label>
		<Button
			v-if="src || missing"
			variant="ghost"
			text="danger"
			size="sm"
			class="self-start"
			@click="assignMedia(block.id, '')"
		>
			{{ $t('editor.media.remove') }}
		</Button>
	</div>
</template>

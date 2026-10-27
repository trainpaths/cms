<script setup lang="ts">
import { Button, Modal } from '@trainpaths/nb-ui'

/**
 * nb-ui's `UnsavedChangesDialog` with translated texts (nb-ui's are English only, without props for them): "Do you
 * want to save the changes?" when leaving a form with unsaved changes. Same events and test ids; the parent decides
 * what each answer does (`useUnsavedChanges`). `canSave` false disables Save and shows `blockedHint`.
 */
withDefaults(defineProps<{ saving?: boolean; canSave?: boolean; blockedHint?: string }>(), {
	saving: false,
	canSave: true,
	blockedHint: undefined,
})
const emit = defineEmits<{ save: []; discard: []; cancel: [] }>()
</script>

<template>
	<!-- mounted with v-if by the parent: always open, Esc/backdrop = cancel -->
	<Modal
		:open="true"
		role="alertdialog"
		size="sm"
		:title="$t('admin.unsaved.title')"
		hide-close
		data-testid="unsaved-dialog"
		@close="emit('cancel')"
	>
		{{ canSave ? $t('admin.unsaved.lost') : (blockedHint ?? $t('admin.unsaved.blocked')) }}
		<template #footer>
			<Button
				variant="ghost"
				data-testid="unsaved-cancel"
				@click="emit('cancel')"
			>
				{{ $t('common.cancel') }}
			</Button>
			<Button
				variant="outline"
				border="danger"
				data-testid="unsaved-discard"
				@click="emit('discard')"
			>
				{{ $t('admin.unsaved.discard') }}
			</Button>
			<Button
				:loading="saving"
				:disabled="!canSave"
				data-testid="unsaved-save"
				@click="emit('save')"
			>
				{{ $t('common.save') }}
			</Button>
		</template>
	</Modal>
</template>

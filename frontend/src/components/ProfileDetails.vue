<script setup lang="ts">
/**
 * Account details (display name edit, roles, change password, log out): the Profile tab of views/Profile.vue (staff)
 * and public/screens/UserProfile.vue (customers). `logged-out`: the parent decides where to go.
 */
import { ref, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '../stores/auth'
import { storeToRefs } from 'pinia'
import { Button, Input, FormField, Card, DescriptionList, DescriptionItem, Alert, Link } from '@trainpaths/nb-ui'

defineProps<{ passwordTo: string }>()
const emit = defineEmits<{ 'logged-out': [] }>()

const authStore = useAuthStore()
const { t } = useI18n()
const { user, authType, error, loading } = storeToRefs(authStore)

const editMode = ref(false)
const editDisplayName = ref('')
const saving = ref(false)
const successMessage = ref('')

onMounted(() => {
	authStore.fetchCurrentUser()
})

function startEdit() {
	editDisplayName.value = user.value?.displayName ?? ''
	editMode.value = true
	successMessage.value = ''
}

function cancelEdit() {
	editMode.value = false
	authStore.clearError()
}

async function saveProfile() {
	saving.value = true
	successMessage.value = ''
	try {
		const success = await authStore.updateProfile(editDisplayName.value)
		if (success) {
			editMode.value = false
			successMessage.value = t('account.updated')
			await authStore.fetchCurrentUser()
		}
	} finally {
		saving.value = false
	}
}

async function handleLogout() {
	await authStore.logout()
	emit('logged-out')
}
</script>

<template>
	<div>
		<div
			v-if="loading && !user"
			class="text-black opacity-60"
		>
			{{ $t('account.loading') }}
		</div>

		<div v-else-if="user">
			<Alert
				v-if="successMessage"
				type="success"
				class="mb-16"
			>
				{{ successMessage }}
			</Alert>

			<Card v-if="editMode">
				<form
					class="flex flex-col gap-8"
					@submit.prevent="saveProfile"
				>
					<FormField
						:label="$t('account.displayName')"
						:error="error || undefined"
					>
						<Input
							v-model="editDisplayName"
							:invalid="!!error"
						/>
					</FormField>
					<div class="flex gap-8">
						<Button
							type="submit"
							:loading="saving"
						>
							{{ saving ? $t('common.saving') : $t('common.save') }}
						</Button>
						<Button
							type="button"
							variant="outline"
							text="primary"
							@click="cancelEdit"
						>
							{{ $t('common.cancel') }}
						</Button>
					</div>
				</form>
			</Card>

			<Card v-else>
				<DescriptionList>
					<DescriptionItem :label="$t('common.email')">{{ user.email }}</DescriptionItem>
					<DescriptionItem :label="$t('account.displayName')">
						{{ user.displayName ?? '—' }}
						<Button
							variant="ghost"
							size="sm"
							class="ml-8"
							@click="startEdit"
						>
							{{ $t('common.edit') }}
						</Button>
					</DescriptionItem>
					<DescriptionItem :label="$t('account.type')">{{ authType }}</DescriptionItem>
					<DescriptionItem :label="$t('account.userType')">{{ user.userType ?? '—' }}</DescriptionItem>
					<DescriptionItem :label="$t('account.roles')">{{ user.roles?.join(', ') || '—' }}</DescriptionItem>
					<DescriptionItem :label="$t('account.organization')">{{ user.organizationId ?? '—' }}</DescriptionItem>
				</DescriptionList>
			</Card>

			<div class="flex items-center gap-16 mt-16">
				<Link :to="passwordTo">{{ $t('account.changePassword') }}</Link>
				<Button @click="handleLogout">{{ $t('account.logout') }}</Button>
			</div>
		</div>

		<div v-else>
			<p>{{ $t('account.loadFailed') }}</p>
			<Alert
				v-if="error"
				type="error"
				class="mt-8"
			>
				{{ error }}
			</Alert>
		</div>
	</div>
</template>

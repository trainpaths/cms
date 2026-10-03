<script setup lang="ts">
import { computed } from 'vue'
import {
	PageContent,
	SiteConfigEntry,
	filledEntries,
	firmName as firmNameOf,
	type TemplateProps,
} from '@trainpaths/cms/site'

/**
 * Template page example (cms.config.json: "template": "imprint"): the owner's blocks in the default layout, followed
 * by the site config the owner fills in under Configuration: firm name, contact, VAT ID (instance field) and the
 * social media links (instance group).
 */
const props = defineProps<TemplateProps>()

const firmName = computed(() => firmNameOf(props.config))
const vatId = computed(() => props.config?.fields.vatId ?? '')
const contact = computed(() => filledEntries(props.config, 'contact'))
const socials = computed(() => filledEntries(props.config, 'socials'))
const empty = computed(() => !firmName.value && !vatId.value && !contact.value.length && !socials.value.length)
</script>

<template>
	<PageContent
		:title="page.title"
		:blocks="page.blocks"
		:media="page.media"
	>
		<template #after>
			<dl
				class="m-0 mt-24 grid grid-cols-[max-content_1fr] gap-x-24 gap-y-8 border-t border-gray-200 px-16 pt-24 text-sm"
				data-testid="imprint-details"
			>
				<template v-if="firmName">
					<dt class="font-medium text-gray-500">Company</dt>
					<dd class="m-0 text-gray-800">{{ firmName }}</dd>
				</template>
				<template
					v-for="entry in contact"
					:key="entry.id"
				>
					<dt class="font-medium text-gray-500">{{ entry.label }}</dt>
					<dd class="m-0 text-gray-800">
						<SiteConfigEntry :entry="entry" />
					</dd>
				</template>
				<template v-if="vatId">
					<dt class="font-medium text-gray-500">VAT ID</dt>
					<dd class="m-0 text-gray-800">{{ vatId }}</dd>
				</template>
				<template v-if="socials.length">
					<dt class="font-medium text-gray-500">Follow us</dt>
					<dd
						class="m-0 flex flex-wrap gap-12"
						data-testid="imprint-socials"
					>
						<a
							v-for="social in socials"
							:key="social.id"
							:href="social.value"
							class="text-primary"
							target="_blank"
							rel="noopener noreferrer"
							>{{ social.label }}</a
						>
					</dd>
				</template>
				<dd
					v-if="empty"
					class="col-span-2 m-0 text-gray-400"
				>
					Fill in the details under Configuration.
				</dd>
			</dl>
		</template>
	</PageContent>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, provide, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Link, Loading, Select } from '@trainpaths/nb-ui'
import { getApiPagesById, getApiPublicState } from '../../api/sdk.gen'
import type { PublicStateResponse } from '../../api/types.gen'
import type { PageDetail } from '../../lib/web-editor'
import PageView from '../PageView.vue'
import SiteFooter from '../../components/SiteFooter.vue'
import SiteHeader from '../../components/SiteHeader.vue'
import { usePublicMenusStore } from '../../stores/menus'
import { useSiteConfigStore } from '../../stores/siteConfig'
import { DEFAULT_LANGUAGE, IS_LOCALIZED, LANGUAGES, languageLabel, languageOf } from '../../i18n/languages'
import { LANGUAGE_SWITCH } from '../../i18n'
import { localizeBlocks, localizeText } from '../../lib/web-editor/core/localize'

const props = defineProps<{ id: string }>()

const page = ref<PageDetail | null>(null)
// the saved working copy in one language, like the public site will show it (`?lang=` from the editor)
const route = useRoute()
const router = useRouter()
const lang = ref(languageOf(route.query.lang).code)
const languageOptions = LANGUAGES.map(({ code }) => ({ value: code, label: languageLabel(code) }))
const localized = computed(() =>
	page.value
		? {
				...localizeText(page.value, page.value.translations, lang.value, DEFAULT_LANGUAGE.code),
				blocks: localizeBlocks(page.value.blocks, lang.value, DEFAULT_LANGUAGE.code),
			}
		: null,
)
const failed = ref(false)
const siteConfig = useSiteConfigStore()
const menus = usePublicMenusStore()
menus.load('main')

// menu + footer link titles in the previewed language (the stores keep the default's: the admin shares them)
const surroundings = ref<PublicStateResponse | null>(null)
watch(
	lang,
	async (code) => {
		surroundings.value = null
		// reload keeps the language (default: no query, like the editor's link)
		const query = code === DEFAULT_LANGUAGE.code ? undefined : code
		if (route.query.lang !== query) void router.replace({ query: { ...route.query, lang: query } })
		if (code === DEFAULT_LANGUAGE.code) return
		try {
			const { data } = await getApiPublicState({ query: { lang: code } })
			if (lang.value === code) surroundings.value = data
		} catch {
			// the default's titles
		}
	},
	{ immediate: true },
)
const menu = computed(() => (surroundings.value ? (surroundings.value.menu ?? null) : (menus.menus.main ?? null)))
const config = computed(() => surroundings.value?.config ?? siteConfig.config)

// the footer's language dropdown switches the preview, not the admin's language
provide(LANGUAGE_SWITCH, {
	current: computed(() => languageOf(lang.value)),
	languages: LANGUAGES,
	change: (code) => {
		lang.value = code
	},
})

onMounted(async () => {
	siteConfig.load()
	try {
		page.value = (await getApiPagesById({ path: { id: props.id } })).data
	} catch {
		failed.value = true
	}
})
</script>

<template>
	<div class="border-b border-yellow-200 bg-yellow-50 px-16 py-8 text-center text-sm text-yellow-800 font-sans">
		{{ page?.status === 'draft' ? $t('admin.preview.draft') : $t('admin.preview.label') }} &middot;
		<Link :to="`/admin/pages/${id}`">{{ $t('admin.preview.back') }}</Link>
		<template v-if="IS_LOCALIZED">
			&middot;
			<Select
				v-model="lang"
				:options="languageOptions"
				size="sm"
				class="inline-flex w-auto align-middle"
				:aria-label="$t('editor.translations.previewLanguage')"
				data-testid="preview-language"
			/>
		</template>
	</div>
	<div
		v-if="localized"
		class="site-theme"
		:lang="languageOf(lang).tag"
	>
		<SiteHeader
			:config="config"
			:menu="menu"
		/>
		<PageView
			:page="localized"
			:config="config"
		/>
		<SiteFooter :config="config" />
	</div>
	<p
		v-else-if="failed"
		class="p-32 text-center text-gray-500"
	>
		{{ $t('admin.preview.notFound') }}
	</p>
	<Loading
		v-else
		:label="$t('admin.preview.loading')"
	/>
</template>

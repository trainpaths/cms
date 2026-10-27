export declare const DEFAULT_LANG: string

/** Primary subtag of a language tag: `de-CH` → `de` (URL prefix, cookie value, message file). */
export declare const codeOf: (tag: string) => string

/** Language tags of the app's `cms.config.json` (`site.lang`), default first (`['en']` without one); throws when invalid. */
export declare function readSiteLanguages(root: string): string[]

/** The default language's tag of the app's `cms.config.json` (default `en`); throws when invalid. */
export declare function readSiteLang(root: string): string

/** Language tags from cms.config.json content (comments + trailing commas allowed), default first; throws when invalid. */
export declare function siteLanguages(json: string, source?: string): string[]

/** The default language's tag from cms.config.json content; throws when invalid. */
export declare function siteLang(json: string, source?: string): string

/** The parts of cms.config.json the build reads (the API validates the rest). */
export interface CmsConfigJson {
	site?: { lang?: unknown }
	theme?: { colors?: unknown }
}

/** cms.config.json content → object (comments + trailing commas allowed); throws with `source` in the message. */
export declare function parseConfig(json: string, source: string): CmsConfigJson

/** Sets `lang` on the `<html>` tag, replacing the template's own. */
export declare const setHtmlLang: (html: string, lang: string) => string

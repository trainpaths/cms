export declare const DEFAULT_LANG: string

/** `site.lang` of the app's `cms.config.json` (default `en`); throws on an invalid tag. */
export declare function readSiteLang(root: string): string

/** `site.lang` from cms.config.json content (comments + trailing commas allowed); throws on an invalid tag. */
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

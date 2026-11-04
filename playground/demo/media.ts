import type { MediaItem, MediaRef } from '@trainpaths/cms/src/api/types.gen.ts'
import blocks from './images/blocks.svg'
import city from './images/city.svg'
import forest from './images/forest.svg'
import lake from './images/lake.svg'
import mountains from './images/mountains.svg'
import waves from './images/waves.svg'

// fixed library: the demo has no upload, the image picker chooses from these
const CREATED = '2026-01-05T09:00:00Z'

function item(id: string, url: string, fileName: string, alt: string, size: number): MediaItem {
	return { id, url, fileName, contentType: 'image/svg+xml', size, alt, createdAt: CREATED }
}

export const MEDIA: MediaItem[] = [
	item('media-lake', lake, 'lake-sunset.svg', 'A lake at sunset with hills on the horizon', 1432),
	item('media-mountains', mountains, 'mountains.svg', 'Snowy mountains above green hills', 828),
	item('media-city', city, 'city-night.svg', 'A city skyline at night with lit windows', 1725),
	item('media-forest', forest, 'forest.svg', 'A pine forest in soft light', 1088),
	item('media-blocks', blocks, 'editor-sketch.svg', 'Sketch of a page built from blocks', 1511),
	item('media-waves', waves, 'waves.svg', 'Teal and amber waves', 604),
]

export const toRef = (media: MediaItem): MediaRef => ({ id: media.id, url: media.url, alt: media.alt })

export const mediaById = (id: string): MediaItem | undefined => MEDIA.find((m) => m.id === id)

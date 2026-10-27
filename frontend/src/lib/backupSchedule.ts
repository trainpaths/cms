import type { BackupInterval, BackupSettingsResponse } from '../api/types.gen'

/** Backup schedule labels, shared by the backups panel (form) and the dashboard summary. */

type Translate = (key: string, named?: Record<string, unknown>) => string

const INTERVALS: BackupInterval[] = ['off', 'daily', 'weekly', 'biweekly', 'monthly']

export const backupIntervals = (t: Translate): { value: BackupInterval; label: string }[] =>
	INTERVALS.map((value) => ({ value, label: t(`admin.backups.intervals.${value}`) }))

/** Weekday 0-6 (Sunday first, like the API) in the language `tag`. */
export const weekdayName = (weekday: number, tag: string) =>
	new Intl.DateTimeFormat(tag, { weekday: 'long', timeZone: 'UTC' }).format(new Date(Date.UTC(2023, 0, 1 + weekday)))

/** "Weekly on Monday, 03:00 UTC"; "Off" when no automatic backups run. */
export function scheduleSummary(
	s: Pick<BackupSettingsResponse, 'interval' | 'timeOfDay' | 'weekday' | 'dayOfMonth'>,
	t: Translate,
	tag: string,
) {
	const interval = t(`admin.backups.intervals.${s.interval}`)
	const time = s.timeOfDay
	if (s.interval === 'off') return interval
	if (s.interval === 'weekly' || s.interval === 'biweekly')
		return t('admin.backups.summaryWeekday', { interval, day: weekdayName(s.weekday, tag), time })
	if (s.interval === 'monthly') return t('admin.backups.summaryMonthDay', { interval, day: s.dayOfMonth, time })
	return t('admin.backups.summaryDaily', { interval, time })
}

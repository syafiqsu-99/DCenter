import { fmtDate } from '@/utils/consumables'

const PAGE = 200
const LIMIT = 2000

export async function loadAllPages(fetchPage) {
  const items = []
  for (;;) {
    const page = await fetchPage(items.length, PAGE)
    items.push(...page.items)
    if (!page.items.length || items.length >= page.total || items.length >= LIMIT) return items
  }
}

export function rangeText(from, to) {
  if (from && to) return `${fmtDate(from)} – ${fmtDate(to)}`
  if (from) return `From ${fmtDate(from)}`
  if (to) return `Up to ${fmtDate(to)}`
  return 'All dates'
}

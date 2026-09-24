export const COLUMN = {
  requestor: 'Requestor',
  receivedBy: 'Received By',
  date: 'Date',
  source: 'Source',
  brand: 'Electrode Brand',
  diameter: 'Electrode Diameter (mm)',
  spec: 'Electrode Specification',
  lot: 'Lot\\ heat Number',
  diaSpec: 'Dia & Spec',
  receiveQty: 'Receive Qty (KG)',
  take: 'Take/KG',
  balance: 'BALANCE',
  type: 'Consumable Type',
  minStock: 'Min Stock (KG)',
  activatedMin: 'Activated Min (KG)',
  finishThreshold: 'Finish threshold (KG)',
  normal: 'Normal (KG)',
  activated: 'Activated (KG)',
  baking: 'Baking (KG)',
  total: 'Total (KG)',
  welder: 'Welder',
}

export const ALL = 'All'
export const ELECTRODE = 'Electrode Filler'
export const STAGE = { normal: 'Normal', baking: 'Baking', activated: 'Activated' }
export const QUICK_QTY = [0.5, 1, 2, 5]
export const UNASSIGNED = 'Unassigned'

export const TXN_LABELS = {
  Receive: 'Receive',
  Transfer: 'Transfer',
  SendToBake: 'To Baking',
  Hold: 'Hold',
  Move: 'Move',
  Issue: 'Pickup',
  Return: 'Return',
  Finish: 'Finished',
  Adjust: 'Adjust',
  Dispose: 'Dispose',
  Void: 'Void',
}

export const TXN_COLORS = {
  Receive: 'success',
  Transfer: 'primary',
  Issue: 'info',
  Return: 'teal',
  Finish: 'deep-orange',
  Adjust: 'warning',
  SendToBake: 'purple',
  Hold: 'indigo',
  Move: 'blue-grey',
  Void: 'error',
}

export const BAKING_LABELS = {
  Queued: 'Queued',
  Baking: 'Baking',
  Baked: 'Baked',
  RebakeQueued: 'Re-bake queued',
  Rebaking: 'Re-baking',
  Rebaked: 'Re-baked',
  Closed: 'Closed',
  Cancelled: 'Cancelled',
}

export const BAKING_COLORS = {
  Queued: 'grey',
  Baking: 'deep-orange',
  Baked: 'success',
  RebakeQueued: 'purple',
  Rebaking: 'deep-orange',
  Rebaked: 'success',
  Closed: 'blue-grey',
  Cancelled: 'error',
}

export const TXN_FILTER_TYPES = ['Receive', 'Transfer', 'SendToBake', 'Hold', 'Move', 'Issue', 'Return', 'Finish', 'Adjust', 'Void']

const kgFormat = new Intl.NumberFormat('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })

export function kg(value) {
  return kgFormat.format(Number(value ?? 0))
}

export function formatDiameter(value) {
  const raw = (value ?? '').toString().trim().toLowerCase().replace('mm', '').replace(',', '.').replace(/\s+/g, '')
  const n = Number(raw)
  return raw && Number.isFinite(n) && n > 0 && n < 100 ? n.toFixed(2) : value
}

export function todayIso() {
  const d = new Date()
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function monthStartIso() {
  return `${todayIso().slice(0, 8)}01`
}

export function fmtDate(iso) {
  if (!iso) return ''
  const [y, m, d] = iso.slice(0, 10).split('-')
  return `${Number(d)}/${Number(m)}/${y}`
}

export function fmtDateTime(iso) {
  return iso ? new Date(iso).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' }) : ''
}

export function fmtTime(iso) {
  return iso ? new Date(iso).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }) : ''
}

export function stageFlow(t) {
  const side = (stage, bin) => (stage ? `${stage}${bin ? ` (${bin})` : ''}` : '—')
  return `${side(t.fromStage, t.fromCompartment)} → ${side(t.toStage, t.toCompartment)}`
}

export function elapsed(fromIso, toIso) {
  if (!fromIso) return ''
  const minutes = Math.max(0, Math.round(((toIso ? new Date(toIso) : new Date()) - new Date(fromIso)) / 60000))
  const h = Math.floor(minutes / 60)
  return h ? `${h}h ${minutes % 60}m` : `${minutes}m`
}

export function toLocalInput(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

export function fromLocalInput(value) {
  return value ? `${value}:00` : null
}

export function categoryParam(value) {
  return value && value !== ALL ? value : undefined
}

export function errorText(e, fallback) {
  const data = e?.response?.data
  if (typeof data === 'string' && data.trim()) return data
  if (data?.errors) return Object.values(data.errors).flat().join(' ')
  if (data?.title) return data.title
  return fallback
}

export function debounce(fn, ms = 250) {
  let timer = null
  return (...args) => {
    clearTimeout(timer)
    timer = setTimeout(() => fn(...args), ms)
  }
}

export function csvCell(value) {
  const text = String(value ?? '')
  return /[",\r\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

export function downloadCsv(fileName, rows) {
  const content = '\uFEFF' + rows.map((r) => r.map(csvCell).join(',')).join('\r\n')
  const url = URL.createObjectURL(new Blob([content], { type: 'text/csv;charset=utf-8' }))
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

export function openPrint(router, kind, query = {}) {
  const clean = Object.fromEntries(Object.entries(query).filter(([, v]) => v !== null && v !== undefined && v !== ''))
  window.open(router.resolve({ name: 'consumable-print', params: { kind }, query: clean }).href, '_blank')
}

export function daysSince(iso) {
  if (!iso) return null
  const [y, m, d] = iso.split('-').map(Number)
  const today = new Date()
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate())
  return Math.round((start - new Date(y, m - 1, d)) / 86400000)
}

export function saveBlob(blob, fileName) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

import { defineStore } from 'pinia'
import api from '@/utils/api'
import { daysAgoIso } from '@/utils/date'
import { useLookupStore } from '@/store/lookupStore'
import { ALL, categoryParam, monthStartIso, todayIso } from '@/utils/consumables'

const SUPERVISOR_KEY = 'dcenter.consumables.supervisor'
const SUPERVISOR_HEADER = 'X-Supervisor-Token'
const HEADER_KEY = 'dcenter.consumables.receiveHeader'
const PIC_KEY = 'dcenter.consumables.personInCharge'
const ENTERED_BY_HEADER = 'X-Entered-By'

function readLocal(key, fallback) {
  try {
    const raw = localStorage.getItem(key)
    return raw ? JSON.parse(raw) : fallback
  } catch {
    return fallback
  }
}

function writeLocal(key, value) {
  try {
    localStorage.setItem(key, JSON.stringify(value))
    return true
  } catch {
    return false
  }
}

function applyEnteredBy(name) {
  const value = (name ?? '').trim()
  if (value) api.defaults.headers[ENTERED_BY_HEADER] = encodeURIComponent(value)
  else delete api.defaults.headers[ENTERED_BY_HEADER]
}

function applySupervisorToken(token) {
  if (token) api.defaults.headers[SUPERVISOR_HEADER] = token
  else delete api.defaults.headers[SUPERVISOR_HEADER]
}

function readSession() {
  try {
    const raw = sessionStorage.getItem(SUPERVISOR_KEY)
    const session = raw ? JSON.parse(raw) : null
    return session && new Date(session.expiresAt) > new Date() ? session : null
  } catch {
    return null
  }
}

function writeSession(session) {
  try {
    if (session) sessionStorage.setItem(SUPERVISOR_KEY, JSON.stringify(session))
    else sessionStorage.removeItem(SUPERVISOR_KEY)
  } catch {
    return
  }
}

const SUPERVISOR_RENEW_MS = 60 * 60 * 1000
let refreshing = false

function fingerprint(text) {
  let hash = 0x811c9dc5
  for (let i = 0; i < text.length; i++) {
    hash ^= text.charCodeAt(i)
    hash = Math.imul(hash, 0x01000193) >>> 0
  }
  return hash.toString(16).padStart(8, '0')
}

function submitHeaders(key, url, payload) {
  return key ? { 'Idempotency-Key': `${key}:${fingerprint(`${url}|${JSON.stringify(payload ?? null)}`)}` } : undefined
}

const requestIds = {}
let generation = 0

function beginRequest(key) {
  requestIds[key] = (requestIds[key] ?? 0) + 1
  return { key, id: requestIds[key], generation }
}

const isLatest = (ticket) => requestIds[ticket.key] === ticket.id

function matches(terms, values) {
  return terms.every((t) => values.some((v) => (v ?? '').toString().toLowerCase().includes(t)))
}

const HTML_RESPONSE = 'The server returned a web page instead of the expected data. Rebuild and restart the DCenter server, then try again.'

function ensureNotHtml(data) {
  const isHtml = (typeof Blob !== 'undefined' && data instanceof Blob && (data.type ?? '').includes('text/html'))
    || (typeof data === 'string' && /^\s*<(!doctype|html)/i.test(data))
  if (isHtml) throw { response: { data: HTML_RESPONSE } }
  return data
}

function searchTerms(text) {
  return (text ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
}

export const useConsumableStore = defineStore('consumables', {
  state: () => ({
    catalog: {
      categories: [], sources: [], stages: [], adjustReasons: [],
      ovenTypes: [], finishThresholdKg: 0.5, allowElectrodeDirectTransfer: false, returnWindowDays: 7,
      welderBackdateDays: 7,
    },
    catalogLoaded: false,

    enteredBy: '',
    supervisor: null,
    clock: Date.now(),
    reloginPrompt: false,
    receiveHeader: { source: '', receiver: null },

    todayReceipts: [],
    loadingToday: false,

    balances: [],
    balancesLoaded: false,
    loadingBalances: false,

    lotStock: [],
    lotStockLoaded: false,
    loadingLotStock: false,

    inventoryFilters: { search: '', category: ALL, lowOnly: false, refillOnly: false, includeZero: false, view: 'items' },
    keepInventoryFilters: false,

    counterWelder: null,
    counterCategory: ALL,
    counterItems: [],
    loadingCounter: false,
    welderToday: [],

    historyFilters: {
      from: monthStartIso(), to: todayIso(), type: null, stage: null, category: ALL, q: '', compartmentId: null, compartmentCode: '',
      welder: null,
    },

    dashboard: null,
    dashboardCategory: ALL,
    loadingDashboard: false,

    personInCharge: '',
    bakingBoard: [],
    loadingBaking: false,

    ovenBoard: { ovens: [], unassigned: [] },
    ovenBoardLoaded: false,
    loadingOvens: false,
  }),

  getters: {
    hasEnteredBy: (s) => !!s.enteredBy.trim(),
    isSupervisor: (s) => !!s.supervisor && Date.parse(s.supervisor.expiresAt) > s.clock,
    earliestEntryDate() {
      return this.isSupervisor ? undefined : daysAgoIso(this.catalog.welderBackdateDays ?? 7)
    },

    filteredBalances(s) {
      const f = s.inventoryFilters
      const terms = searchTerms(f.search)
      return s.balances.filter((r) =>
        (f.category === ALL || r.category === f.category) &&
        (!f.lowOnly || r.isLow) &&
        (!f.refillOnly || r.needsRefill) &&
        matches(terms, [r.diaSpec, r.category, r.specification, r.diameter]))
    },

    balanceTotals() {
      return this.filteredBalances.reduce(
        (acc, r) => {
          acc.normalKg += r.normalKg
          acc.bakingKg += r.bakingKg
          acc.activatedKg += r.activatedKg
          acc.totalKg += r.totalKg
          if (r.isLow) acc.lowCount += 1
          return acc
        },
        { normalKg: 0, bakingKg: 0, activatedKg: 0, totalKg: 0, lowCount: 0 },
      )
    },

    filteredLotStock(s) {
      const f = s.inventoryFilters
      const terms = searchTerms(f.search)
      return s.lotStock.filter((r) =>
        (f.category === ALL || r.category === f.category) &&
        (!f.lowOnly || r.isLow) &&
        matches(terms, [r.diaSpec, r.brand, r.lotNumber, r.receivedBy, r.source, r.category]))
    },
  },

  actions: {
    init() {
      this.receiveHeader = { source: '', receiver: null, ...readLocal(HEADER_KEY, {}) }
      this.personInCharge = readLocal(PIC_KEY, '')
      this.supervisor = readSession()
      this.clock = Date.now()
      applySupervisorToken(this.supervisor?.token)
      api.defaults.onUnauthorized = () => {
        if (this.supervisor) this.lockSupervisor()
      }
      this.syncActor()
    },

    tick() {
      this.clock = Date.now()
      if (this.supervisor && !this.isSupervisor) this.lockSupervisor()
    },

    syncActor() {
      this.enteredBy = this.isSupervisor ? this.supervisor.name : this.counterWelder?.welderName ?? ''
      applyEnteredBy(this.enteredBy)
    },

    setWelder(welder) {
      this.counterWelder = welder ?? null
      this.syncActor()
    },

    async unlockSupervisor(name, password) {
      const { data } = await api.post('/supervisor/login', { name, password })
      this.supervisor = { name: data.name, token: data.token, expiresAt: data.expiresAt }
      this.clock = Date.now()
      this.counterWelder = null
      writeSession(this.supervisor)
      applySupervisorToken(data.token)
      this.syncActor()
      return this.supervisor
    },

    async logoutSupervisor() {
      if (this.supervisor) await api.post('/supervisor/logout').catch(() => {})
      this.lockSupervisor()
    },

    lockSupervisor() {
      this.supervisor = null
      this.counterWelder = null
      writeSession(null)
      applySupervisorToken(null)
      this.dashboard = null
      this.syncActor()
    },

    async refreshSupervisorIfNeeded() {
      if (!this.supervisor || refreshing) return
      if (Date.parse(this.supervisor.expiresAt) - Date.now() > SUPERVISOR_RENEW_MS) return
      refreshing = true
      try {
        const { data } = await api.post('/supervisor/session/refresh')
        if (!this.supervisor || !data?.token) return
        this.supervisor = { name: data.name, token: data.token, expiresAt: data.expiresAt }
        this.clock = Date.now()
        writeSession(this.supervisor)
        applySupervisorToken(data.token)
      } catch {
        // 401 locks the session through onUnauthorized; network errors retry on the next check
      } finally {
        refreshing = false
      }
    },

    async verifySupervisor() {
      if (!this.supervisor) return false
      try {
        await api.get('/supervisor/session')
        return true
      } catch {
        this.lockSupervisor()
        return false
      }
    },

    async loadMonthConsumption(month) {
      const { data } = await api.get('/consumables/dashboard/consumption', {
        params: { month, category: categoryParam(this.dashboardCategory) },
      })
      return data
    },

    async loadNormalStock(category) {
      const { data } = await api.get('/consumables/normal-stock', { params: { category } })
      return data
    },

    async loadPasswordStatus() {
      const { data } = await api.get('/supervisor/password')
      return data
    },

    async changePassword(currentPassword, newPassword) {
      const { data } = await api.post('/supervisor/password', { currentPassword, newPassword })
      this.reloginPrompt = true
      this.lockSupervisor()
      return data
    },

    async exportItems(template = false) {
      const { data } = await api.get('/consumables/items/export', { params: { template }, responseType: 'blob' })
      return ensureNotHtml(data)
    },

    async importItems(file, { commit = false, skipInvalid = false } = {}) {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post('/consumables/items/import', form, { params: { commit, skipInvalid } })
      ensureNotHtml(data)
      if (data.committed) {
        this.stale()
        useLookupStore().load(true).catch(() => {})
      }
      return data
    },

    async stockTemplate() {
      const { data } = await api.get('/consumables/stock-import/template', { responseType: 'blob' })
      return ensureNotHtml(data)
    },

    async importStock(file, { commit = false, skipInvalid = false, key = null } = {}) {
      const form = new FormData()
      form.append('file', file)
      const headers = commit ? submitHeaders(key, '/consumables/stock-import', [file.name, file.size, file.lastModified, skipInvalid]) : undefined
      const { data } = await api.post('/consumables/stock-import', form, { params: { commit, skipInvalid }, headers })
      ensureNotHtml(data)
      if (data.committed) {
        this.stale()
        useLookupStore().load(true).catch(() => {})
      }
      return data
    },

    async deleteItem(id) {
      await api.delete(`/consumables/items/${id}`)
      this.stale()
    },

    rememberReceiveHeader(patch) {
      this.receiveHeader = { ...this.receiveHeader, ...patch }
      writeLocal(HEADER_KEY, this.receiveHeader)
    },

    async loadCatalog() {
      if (this.catalogLoaded) return
      const { data } = await api.get('/consumables/catalog')
      this.catalog = data
      this.catalogLoaded = true
    },

    stale() {
      generation += 1
      this.balancesLoaded = false
      this.lotStockLoaded = false
      this.ovenBoardLoaded = false
    },

    rememberPersonInCharge(name) {
      this.personInCharge = name ?? ''
      writeLocal(PIC_KEY, this.personInCharge)
    },

    async searchItems(q, { category, activeOnly = true, take = 50 } = {}) {
      const { data } = await api.get('/consumables/items', {
        params: { q, category: categoryParam(category), activeOnly, take },
      })
      return data
    },

    async saveItem(item) {
      const body = {
        category: item.category,
        specification: item.specification,
        diameter: item.diameter,
        minStockKg: Number(item.minStockKg) || 0,
        activatedMinKg: Number(item.activatedMinKg) || 0,
        finishThresholdKg: item.finishThresholdKg === '' || item.finishThresholdKg === null || item.finishThresholdKg === undefined
          ? null
          : Number(item.finishThresholdKg),
        isActive: item.isActive ?? true,
        holdingOvenType: item.category === 'Electrode Filler' ? item.holdingOvenType || null : null,
      }
      const { data } = item.id
        ? await api.put(`/consumables/items/${item.id}`, body)
        : await api.post('/consumables/items', body)
      this.stale()
      useLookupStore().load(true).catch(() => {})
      return data
    },

    async lotsFor(itemId) {
      const { data } = await api.get(`/consumables/items/${itemId}/lots`)
      return data
    },

    async lotBalances(itemId, includeZero = false) {
      const { data } = await api.get(`/consumables/items/${itemId}/lot-balances`, { params: { includeZero } })
      return data
    },

    async recentQuantities(itemId, type = 'Receive') {
      const { data } = await api.get(`/consumables/items/${itemId}/recent-quantities`, { params: { type } })
      return data
    },

    async loadBalances(force = false) {
      if (this.balancesLoaded && !force) return
      const ticket = beginRequest('balances')
      this.loadingBalances = true
      try {
        const { data } = await api.get('/consumables/balances', { params: { includeZero: true } })
        if (!isLatest(ticket)) return
        this.balances = data
        this.balancesLoaded = ticket.generation === generation
      } finally {
        if (isLatest(ticket)) this.loadingBalances = false
      }
    },

    async loadLotStock(force = false) {
      if (this.lotStockLoaded && !force) return
      const ticket = beginRequest('lotStock')
      this.loadingLotStock = true
      try {
        const { data } = await api.get('/consumables/lot-stock', {
          params: { includeZero: this.inventoryFilters.includeZero },
        })
        if (!isLatest(ticket)) return
        this.lotStock = data
        this.lotStockLoaded = ticket.generation === generation
      } finally {
        if (isLatest(ticket)) this.loadingLotStock = false
      }
    },

    async loadToday() {
      this.loadingToday = true
      try {
        const { data } = await api.get('/consumables/transactions/today', { params: { type: 'Receive' } })
        this.todayReceipts = data
      } finally {
        this.loadingToday = false
      }
    },

    async searchWelders(q, stockOnly = true) {
      const { data } = await api.get('/welders/search', { params: { q, stockOnly } })
      return data
    },

    async registerWelder(welder) {
      const { data } = await api.post('/welders', welder)
      return data
    },

    async loadCounter() {
      const ticket = beginRequest('counter')
      this.loadingCounter = true
      try {
        const welderId = this.counterWelder?.id
        const [items, today] = await Promise.all([
          api.get('/consumables/counter', { params: { welderId, category: categoryParam(this.counterCategory) } }),
          welderId ? api.get(`/consumables/counter/welders/${welderId}/today`) : Promise.resolve({ data: [] }),
        ])
        if (!isLatest(ticket)) return
        this.counterItems = items.data
        this.welderToday = today.data
      } finally {
        if (isLatest(ticket)) this.loadingCounter = false
      }
    },

    async post(url, payload, key = null) {
      const { data } = await api.post(url, payload, { headers: submitHeaders(key, url, payload) })
      this.stale()
      return data
    },

    async receive(payload, key = null) {
      const data = await this.post('/consumables/receive', payload, key)
      if (payload.newItem) useLookupStore().load(true).catch(() => {})
      await this.loadToday().catch(() => {})
      return data
    },

    transfer(payload, key = null) {
      return this.post('/consumables/transfer', payload, key)
    },

    issue(payload, key = null) {
      return this.post('/consumables/issue', payload, key)
    },

    returnStock(payload, key = null) {
      return this.post('/consumables/return', payload, key)
    },

    finish(payload, key = null) {
      return this.post('/consumables/finish', payload, key)
    },

    move(payload, key = null) {
      return this.post('/consumables/move', payload, key)
    },

    async loadCountSheet(scope, category) {
      const { data } = await api.get('/consumables/count-sheet', { params: { scope, category: categoryParam(category) } })
      return data
    },

    async postStockCount(payload, key = null) {
      const data = await this.post('/consumables/stock-counts', payload, key)
      this.dashboard = null
      return data
    },

    async loadStockCounts(params) {
      const { data } = await api.get('/consumables/stock-counts', { params })
      return data
    },

    async loadStockCount(referenceNo) {
      const { data } = await api.get(`/consumables/stock-counts/${encodeURIComponent(referenceNo)}`)
      return data
    },

    async loadBakingBoard() {
      const ticket = beginRequest('bakingBoard')
      this.loadingBaking = true
      try {
        const { data } = await api.get('/consumables/baking/board')
        if (isLatest(ticket)) this.bakingBoard = data
      } finally {
        if (isLatest(ticket)) this.loadingBaking = false
      }
    },

    async sendToBake(payload, key = null) {
      const data = await this.post('/consumables/baking', payload, key)
      this.rememberPersonInCharge(payload.personInCharge)
      useLookupStore().load(true).catch(() => {})
      return data
    },

    startBaking(ids, at = null, key = null) {
      return this.post('/consumables/baking/start', { ids, at }, key)
    },

    stopBaking(ids, at = null, key = null) {
      return this.post('/consumables/baking/stop', { ids, at }, key)
    },

    async updateBaking(id, body) {
      const { data } = await api.put(`/consumables/baking/${id}`, body)
      this.stale()
      this.dashboard = null
      return data
    },

    place(payload, key = null) {
      return this.post('/consumables/holding', payload, key)
    },

    async loadBakingRecords(params) {
      const { data } = await api.get('/consumables/baking', { params })
      return data
    },

    async loadHoldingRecords(params) {
      const { data } = await api.get('/consumables/holding', { params })
      return data
    },

    async loadOvens(force = false) {
      if (this.ovenBoardLoaded && !force) return
      const ticket = beginRequest('ovens')
      this.loadingOvens = true
      try {
        const { data } = await api.get('/consumables/ovens')
        if (!isLatest(ticket)) return
        this.ovenBoard = data
        this.ovenBoardLoaded = ticket.generation === generation
      } finally {
        if (isLatest(ticket)) this.loadingOvens = false
      }
    },

    async voidTransaction(txnNo, remarks, key = null) {
      const data = await this.post(`/consumables/transactions/${encodeURIComponent(txnNo)}/void`, { remarks }, key)
      this.dashboard = null
      return data
    },

    async loadTransactions(params) {
      const { data } = await api.get('/consumables/transactions', { params })
      return data
    },

    async loadDashboard(category = this.dashboardCategory) {
      this.dashboardCategory = category
      this.loadingDashboard = true
      try {
        const { data } = await api.get('/consumables/dashboard', { params: { category: categoryParam(category) } })
        if (this.dashboardCategory === category) this.dashboard = data
      } finally {
        this.loadingDashboard = false
      }
    },

    showHistory(patch) {
      this.historyFilters = {
        from: monthStartIso(), to: todayIso(), type: null, stage: null, category: ALL, q: '', compartmentId: null, compartmentCode: '',
        welder: null,
        ...patch,
      }
    },

    showInventory(patch) {
      Object.assign(this.inventoryFilters,
        { search: '', category: ALL, lowOnly: false, refillOnly: false, includeZero: false, view: 'items' }, patch)
      this.keepInventoryFilters = true
    },

    enterStock() {
      if (!this.keepInventoryFilters) {
        Object.assign(this.inventoryFilters, { search: '', category: ALL, lowOnly: false, refillOnly: false })
      }
      this.keepInventoryFilters = false
    },
  },
})

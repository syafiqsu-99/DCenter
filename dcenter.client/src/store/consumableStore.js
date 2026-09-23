import { defineStore } from 'pinia'
import api from '@/utils/api'
import { useLookupStore } from '@/store/lookupStore'
import { ALL, monthStartIso, todayIso } from '@/utils/consumables'

const GROUP_KEYS = {
  consumable: (r) => `${r.diaSpec} · ${r.brand} · ${r.consumableType}`,
  brand: (r) => r.brand,
  spec: (r) => r.specification,
  diameter: (r) => r.diameter,
  lot: (r) => `${r.lotNumber} — ${r.diaSpec} · ${r.brand}`,
}

function distinctSorted(values) {
  return [...new Set(values.filter(Boolean))].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }))
}

function refreshLookups() {
  useLookupStore().load(true)
}

export const useConsumableStore = defineStore('consumables', {
  state: () => ({
    catalog: { types: [], locations: [] },
    catalogLoaded: false,

    header: { receiveLocation: '', issueLocation: '', receivedBy: '', requestor: '' },

    today: { Receive: [], Issue: [] },
    loadingToday: { Receive: false, Issue: false },

    stock: [],
    stockLocation: '',
    loadingStock: false,

    stockCard: [],
    stockCardLoaded: false,
    loadingStockCard: false,
    filters: { search: '', type: null, brand: null, diameter: null, location: ALL, includeZero: false, lowOnly: false, view: 'card' },

    historyFilters: { from: monthStartIso(), to: todayIso(), type: null, location: ALL, q: '' },

    dashboard: null,
    dashboardLocation: ALL,
    loadingDashboard: false,
  }),

  getters: {
    brandOptions: (s) => distinctSorted(s.stockCard.map((r) => r.brand)),
    diameterOptions: (s) => distinctSorted(s.stockCard.map((r) => r.diameter)),

    filteredCard(s) {
      const f = s.filters
      const terms = (f.search ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean)
      return s.stockCard.filter((r) =>
        (!f.type || r.consumableType === f.type) &&
        (!f.brand || r.brand === f.brand) &&
        (!f.diameter || r.diameter === f.diameter) &&
        (f.location === ALL || r.source === f.location) &&
        (!f.lowOnly || r.isLow) &&
        terms.every((t) =>
          [r.brand, r.specification, r.diameter, r.lotNumber, r.requestor, r.consumableType, r.diaSpec]
            .some((v) => (v ?? '').toLowerCase().includes(t))))
    },

    cardTotals() {
      return this.filteredCard.reduce(
        (acc, r) => {
          acc.receivedKg += r.receiveQtyKg
          acc.issuedKg += r.issuedKg
          acc.balanceKg += r.balanceKg
          if (r.isLow) acc.lowConsumables.add(r.consumableId)
          return acc
        },
        { receivedKg: 0, issuedKg: 0, balanceKg: 0, lowConsumables: new Set() },
      )
    },

    summaryRows() {
      const keyOf = GROUP_KEYS[this.filters.view]
      if (!keyOf) return []
      const groups = new Map()
      for (const r of this.filteredCard) {
        const key = keyOf(r)
        const g = groups.get(key) ?? { key, receivedKg: 0, issuedKg: 0, balanceKg: 0, lots: new Set(), isLow: false }
        g.receivedKg += r.receiveQtyKg
        g.issuedKg += r.issuedKg
        g.balanceKg += r.balanceKg
        g.lots.add(`${r.lotId}|${r.source}`)
        g.isLow ||= r.isLow
        groups.set(key, g)
      }
      return [...groups.values()]
        .map(({ lots, ...g }) => ({ ...g, lotCount: lots.size }))
        .sort((a, b) => b.balanceKg - a.balanceKg)
    },
  },

  actions: {
    async loadCatalog() {
      if (this.catalogLoaded) return
      const { data } = await api.get('/consumables/catalog')
      this.catalog = data
      this.catalogLoaded = true
      const first = data.locations[0] ?? ''
      if (!this.header.receiveLocation) this.header.receiveLocation = first
      if (!this.header.issueLocation) this.header.issueLocation = first
    },

    rememberHeader(patch) {
      Object.assign(this.header, patch)
    },

    async searchConsumables(q, { activeOnly = true, take = 30 } = {}) {
      const { data } = await api.get('/consumables', { params: { q, activeOnly, take } })
      return data
    },

    async saveConsumable(item) {
      const body = {
        consumableType: item.consumableType,
        manufacturer: item.manufacturer,
        specification: item.specification,
        diameter: item.diameter,
        minStockKg: Number(item.minStockKg) || 0,
        isActive: item.isActive ?? true,
      }
      const { data } = item.id
        ? await api.put(`/consumables/${item.id}`, body)
        : await api.post('/consumables', body)
      this.stockCardLoaded = false
      refreshLookups()
      return data
    },

    async lotsFor(consumableId) {
      const { data } = await api.get(`/consumables/${consumableId}/lots`)
      return data
    },

    async recentQuantities(consumableId) {
      const { data } = await api.get(`/consumables/${consumableId}/recent-quantities`)
      return data
    },

    async loadStock(location) {
      this.loadingStock = true
      this.stockLocation = location
      try {
        const { data } = await api.get('/consumables/stock', { params: { location } })
        if (this.stockLocation === location) this.stock = data
      } finally {
        this.loadingStock = false
      }
    },

    async loadToday(type) {
      this.loadingToday[type] = true
      try {
        const { data } = await api.get('/consumables/transactions/today', { params: { type } })
        this.today[type] = data
      } finally {
        this.loadingToday[type] = false
      }
    },

    async receive(payload) {
      const { data } = await api.post('/consumables/receive', payload)
      this.stockCardLoaded = false
      if (payload.newConsumable) refreshLookups()
      this.loadToday('Receive')
      return data
    },

    async issue(payload) {
      const { data } = await api.post('/consumables/issue', payload)
      this.stockCardLoaded = false
      await Promise.all([this.loadToday('Issue'), this.loadStock(payload.location)])
      return data
    },

    async voidTransaction(id, remarks) {
      const { data } = await api.post(`/consumables/transactions/${id}/void`, { remarks })
      this.stockCardLoaded = false
      const refresh = [this.loadToday('Receive'), this.loadToday('Issue')]
      if (this.stockLocation) refresh.push(this.loadStock(this.stockLocation))
      await Promise.all(refresh)
      return data
    },

    async loadStockCard(force = false) {
      if (this.stockCardLoaded && !force) return
      this.loadingStockCard = true
      try {
        const { data } = await api.get('/consumables/stock-card', { params: { includeZero: this.filters.includeZero } })
        this.stockCard = data
        this.stockCardLoaded = true
      } finally {
        this.loadingStockCard = false
      }
    },

    async loadTransactions(params) {
      const { data } = await api.get('/consumables/transactions', { params })
      return data
    },

    async loadDashboard(location = this.dashboardLocation) {
      this.dashboardLocation = location
      this.loadingDashboard = true
      try {
        const { data } = await api.get('/consumables/dashboard', { params: { location } })
        if (this.dashboardLocation === location) this.dashboard = data
      } finally {
        this.loadingDashboard = false
      }
    },

    async importOpening(file) {
      const form = new FormData()
      form.append('file', file)
      const { data } = await api.post('/consumables/import', form)
      this.stockCardLoaded = false
      refreshLookups()
      return data
    },

    showHistory(patch) {
      this.historyFilters = { from: monthStartIso(), to: todayIso(), type: null, location: ALL, q: '', ...patch }
    },

    showInventory(patch) {
      Object.assign(this.filters, { search: '', type: null, brand: null, diameter: null, location: ALL, lowOnly: false, view: 'card' }, patch)
    },
  },
})

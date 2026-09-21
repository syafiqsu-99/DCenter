import { defineStore } from 'pinia'
import api from '@/utils/api'
import { useBpvcStore } from '@/store/bpvcStore'
import { useWpsStore } from '@/store/wpsStore'

function blankMaterial(col) {
  return { id: 0, columnNumber: col, process: '', size: '', type: '', manuf: '', heatLot: '' }
}

function jointFromRow(index, row) {
  return {
    id: 0,
    jointNumber: index + 1,
    partDescLeft: row?.itemDesc ?? '',
    partNoLeft: row?.assemblyItem ?? '',
    heatNumberLeft: '',
    partDescRight: row?.componentDesc ?? '',
    partNoRight: row?.childPart ?? '',
    heatNumberRight: '',
    wpsNo: '',
    rev: '',
    welderName: '',
    welderNo: '',
    materials: [blankMaterial(1), blankMaterial(2), blankMaterial(3)],
  }
}

function normalizeJoints(report) {
  for (const j of report.joints ?? []) {
    const byCol = new Map((j.materials ?? []).map((m) => [m.columnNumber, m]))
    j.materials = [1, 2, 3].map((c) => byCol.get(c) ?? blankMaterial(c))
  }
  return report
}

function newReport(workOrderNumber, rows) {
  const first = rows[0] ?? {}
  const today = new Date().toISOString().slice(0, 10)
  return {
    id: 0,
    workOrderNumber,
    reportRequired: true,
    completedAt: null,
    rowVersion: null,
    dateWelded: today,
    partNo: first.assemblyItem ?? '',
    description: first.itemDesc ?? '',
    materialSpec1: '', materialSpec2: '', materialSpec3: '',
    grade1: '', grade2: '', grade3: '',
    pNumber1: '', pNumber2: '', pNumber3: '',
    joints: rows.slice(0, 50).map((row, i) => jointFromRow(i, row)),
  }
}

const SEARCH_DEBOUNCE_MS = 350
const MIN_QUERY_LENGTH = 2
const PAGE_SIZE = 50
let searchTimer = null
let searchToken = 0

export const useReportStore = defineStore('report', {
  state: () => ({
    searchInput: '',
    searchQuery: '',
    searchResults: [],
    hasMoreResults: false,
    loadingRows: false,
    loadingMoreRows: false,
    confirmed: false,
    resolvedWorkOrder: '',
    report: null,
    loading: false,
    saving: false,
    deleting: false,
    conflict: false,
    error: '',
    savedReports: [],
    loadingSaved: false,
    history: [],
    loadingHistory: false,
    savedSnapshot: '',
    reportParts: [],
  }),

  getters: {
    isComplete: (s) => !!s.report?.completedAt,
    hasDateWelded: (s) => !!s.report?.dateWelded,
    jointCount: (s) => s.report?.joints.length ?? 0,
    filterPNos: (s) =>
      [...new Set([s.report?.pNumber1, s.report?.pNumber2, s.report?.pNumber3]
        .map((p) => (p ?? '').trim())
        .filter(Boolean))],
    allowedWps() {
      const pnos = this.filterPNos
      if (!pnos.length) return []
      const seen = new Map()
      for (const w of useWpsStore().items) {
        const p = (w.pNo ?? '').trim()
        if (pnos.includes(p) && !seen.has(w.wpsNo)) {
          seen.set(w.wpsNo, { wpsNo: w.wpsNo, process: w.process, baseMetal: w.baseMetal, pNo: w.pNo })
        }
      }
      return [...seen.values()]
    },
    wpsFilterNote() {
      if (!this.filterPNos.length) return 'Fill in a P# above to narrow the WPS list.'
      if (!this.allowedWps.length) return `No WPS covers P-No ${this.filterPNos.join(', ')}.`
      return null
    },
    loadingWpsFilter: () => useWpsStore().loading,
    filteredRows: (s) => s.searchResults,
    distinctWorkOrders() {
      return [...new Set(this.filteredRows.map((r) => r.workOrderNumber))]
    },
    canSelect() {
      return this.distinctWorkOrders.length === 1
    },
    selectedRows() {
      return this.searchResults.filter((r) => r.workOrderNumber === this.resolvedWorkOrder)
    },
    savedByWorkOrder() {
      return new Map(this.savedReports.map((r) => [r.workOrderNumber, r]))
    },
    partDescOptions() {
      return [
        ...new Set(this.selectedRows.flatMap((r) => [r.itemDesc, r.componentDesc]).filter(Boolean)),
      ]
    },
    partNoOptions() {
      return [
        ...new Set(this.selectedRows.flatMap((r) => [r.assemblyItem, r.childPart]).filter(Boolean)),
      ]
    },
    leftParts() {
      const seen = new Map()
      for (const r of this.reportParts) {
        if (r.assemblyItem && !seen.has(r.assemblyItem)) {
          seen.set(r.assemblyItem, { no: r.assemblyItem, desc: r.itemDesc ?? '' })
        }
      }
      return [...seen.values()]
    },
    rightParts() {
      const seen = new Map()
      for (const r of this.reportParts) {
        if (r.childPart && !seen.has(r.childPart)) {
          seen.set(r.childPart, { no: r.childPart, desc: r.componentDesc ?? '' })
        }
      }
      return [...seen.values()]
    },
    isDirty: (s) => !!s.report && JSON.stringify(s.report) !== s.savedSnapshot,
    needsLeavePrompt() {
      return this.confirmed && this.isDirty && !this.isComplete
    },
  },

  actions: {
    setSearchInput(value) {
      this.searchInput = value ?? ''
      clearTimeout(searchTimer)
      searchTimer = setTimeout(() => this.runSearch(this.searchInput), SEARCH_DEBOUNCE_MS)
    },

    async runSearch(term) {
      const q = (term ?? '').trim()
      const token = ++searchToken
      this.searchQuery = q.length >= MIN_QUERY_LENGTH ? q : ''
      this.loadingRows = true
      this.error = ''
      try {
        const { data } = await api.get('/workorders/search', {
          params: { q: this.searchQuery, skip: 0, take: PAGE_SIZE },
        })
        if (token !== searchToken) return
        this.searchResults = data.items.map((r, i) => ({ ...r, _index: i }))
        this.hasMoreResults = data.hasMore
      } catch (e) {
        if (token !== searchToken) return
        this.error = e.response?.data ?? 'Could not load work orders.'
        this.searchResults = []
        this.hasMoreResults = false
      } finally {
        if (token === searchToken) this.loadingRows = false
      }
    },

    async loadMoreWorkOrders() {
      if (this.loadingRows || this.loadingMoreRows || !this.hasMoreResults) return
      const token = searchToken
      this.loadingMoreRows = true
      try {
        const { data } = await api.get('/workorders/search', {
          params: { q: this.searchQuery, skip: this.searchResults.length, take: PAGE_SIZE },
        })
        if (token !== searchToken) return
        const start = this.searchResults.length
        this.searchResults.push(...data.items.map((r, i) => ({ ...r, _index: start + i })))
        this.hasMoreResults = data.hasMore
      } catch {
        // Keep what's loaded
      } finally {
        if (token === searchToken) this.loadingMoreRows = false
      }
    },

    async loadReportParts(workOrderNumber) {
      const wo = (workOrderNumber ?? '').trim()
      if (!wo) { this.reportParts = []; return }
      try {
        const { data } = await api.get(`/workorders/${encodeURIComponent(wo)}/parts`)
        this.reportParts = data ?? []
      } catch {
        this.reportParts = []
      }
    },

    async loadSavedReports() {
      this.loadingSaved = true
      this.error = ''
      try {
        const { data } = await api.get('/reports')
        this.savedReports = data
      } catch (e) {
        this.error = 'Could not load saved reports.'
        this.savedReports = []
        throw e
      } finally {
        this.loadingSaved = false
      }
    },

    async selectWorkOrder() {
      if (!this.canSelect) return
      this.resolvedWorkOrder = this.distinctWorkOrders[0]
      this.loading = true
      this.error = ''
      try {
        const res = await api.get(`/reports/${encodeURIComponent(this.resolvedWorkOrder)}`)
        this.report =
          res.status === 204 || !res.data
            ? newReport(this.resolvedWorkOrder, this.selectedRows)
            : normalizeJoints(res.data)
        this.confirmed = true
        this.reportParts = this.selectedRows
        this.markPristine()
        this.ensureRefData()
      } catch {
        this.error = 'Could not load the report draft.'
      } finally {
        this.loading = false
      }
    },

    async openReport(workOrderNumber) {
      this.resolvedWorkOrder = workOrderNumber
      this.loading = true
      this.error = ''
      try {
        const res = await api.get(`/reports/${encodeURIComponent(workOrderNumber)}`)
        if (res.status === 204 || !res.data) {
          this.error = 'That report no longer exists.'
          return
        }
        this.report = normalizeJoints(res.data)
        this.confirmed = true
        this.markPristine()
        this.ensureRefData()
        this.loadReportParts(workOrderNumber)
      } catch {
        this.error = 'Could not open the report.'
      } finally {
        this.loading = false
      }
    },

    async reloadReport() {
      if (!this.report?.workOrderNumber) return false
      return this.openReport(this.report.workOrderNumber)
    },

    async save() {
      if (!this.report?.dateWelded) {
        this.error = 'Date welded is required before saving.'
        return false
      }
      this.saving = true
      this.error = ''
      this.conflict = false
      try {
        const { data } = await api.post('/reports', this.report)
        this.report = normalizeJoints(data)
        this.markPristine()
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.conflict = e.response?.status === 409
        this.error = e.response?.data ?? 'Save failed.'
        return false
      } finally {
        this.saving = false
      }
    },

    async setComplete(complete) {
      if (!this.report?.workOrderNumber) return false
      this.error = ''
      try {
        await api.post(`/reports/${encodeURIComponent(this.report.workOrderNumber)}/complete`, complete)
        this.report.completedAt = complete ? new Date().toISOString() : null
        this.markPristine()
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.error = e.response?.data ?? 'Could not update status.'
        return false
      }
    },

    ensureRefData() {
      useBpvcStore().load()
      useWpsStore().load()
    },

    setJointCount(n) {
      const count = Math.min(50, Math.max(1, Math.round(n) || 1))
      const joints = this.report.joints
      if (joints.length > count) joints.length = count
      else while (joints.length < count) joints.push(jointFromRow(joints.length, this.selectedRows[joints.length]))
    },

    async loadHistory() {
      if (!this.report?.workOrderNumber) return
      this.loadingHistory = true
      try {
        const { data } = await api.get(`/reports/${encodeURIComponent(this.report.workOrderNumber)}/history`)
        this.history = data
      } catch {
        this.history = []
      } finally {
        this.loadingHistory = false
      }
    },

    async deleteDraft() {
      if (!this.report?.workOrderNumber) return false
      this.deleting = true
      this.error = ''
      try {
        await api.delete(`/reports/${encodeURIComponent(this.report.workOrderNumber)}`)
        await this.loadSavedReports()
        this.backToList()
        return true
      } catch (e) {
        this.error = e.response?.data ?? 'Could not delete the draft.'
        return false
      } finally {
        this.deleting = false
      }
    },

    backToList() {
      this.confirmed = false
      this.report = null
      this.resolvedWorkOrder = ''
      this.history = []
      this.conflict = false
      this.savedSnapshot = ''
      this.reportParts = []
    },

    async deleteSaved(workOrderNumber) {
      this.error = ''
      try {
        await api.delete(`/reports/${encodeURIComponent(workOrderNumber)}`)
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.error = e.response?.data ?? 'Could not delete the draft.'
        return false
      }
    },

    async duplicateReport(workOrderNumber) {
      this.loading = true
      this.error = ''
      try {
        const res = await api.get(`/reports/${encodeURIComponent(workOrderNumber)}`)
        if (res.status === 204 || !res.data) {
          this.error = 'That report no longer exists.'
          return
        }
        const src = normalizeJoints(res.data)
        const today = new Date().toISOString().slice(0, 10)
        this.report = {
          ...src,
          id: 0,
          rowVersion: null,
          completedAt: null,
          workOrderNumber: '',
          partNo: '',
          description: '',
          dateWelded: today,
          joints: src.joints.map((j) => ({
            ...j,
            id: 0,
            materials: (j.materials ?? []).map((m) => ({ ...m, id: 0 })),
          })),
        }
        this.resolvedWorkOrder = ''
        this.confirmed = true
        this.history = []
        this.conflict = false
        this.markPristine()
        this.ensureRefData()
        this.loadReportParts(workOrderNumber)
      } catch {
        this.error = 'Could not duplicate the report.'
      } finally {
        this.loading = false
      }
    },

    async autofillFromWorkOrder(workOrderNumber) {
      const wo = (workOrderNumber ?? '').trim()
      if (!this.report || this.report.id !== 0 || !wo) return
      this.loadReportParts(wo)
      try {
        const { data } = await api.get(`/workorders/${encodeURIComponent(wo)}/header`)
        if (data && typeof data === 'object') {
          if (data.partNo) this.report.partNo = data.partNo
          if (data.description) this.report.description = data.description
        }
      } catch {
        // no matching work order in the ERP source
      }
    },

    markPristine() {
      this.savedSnapshot = this.report ? JSON.stringify(this.report) : ''
    },
  },
})

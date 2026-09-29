import { defineStore } from 'pinia'
import api from '@/utils/api'
import { useBpvcStore } from '@/store/bpvcStore'
import { useWpsStore } from '@/store/wpsStore'
import { todayIso } from '@/utils/date'
import { errorText } from '@/utils/errors'
import { MAX_JOINTS } from '@/utils/constants'

function blankMaterial(col) {
  return { id: 0, columnNumber: col, process: '', size: '', type: '', manuf: '', heatLot: '' }
}

function blankJoint(index) {
  return {
    id: 0,
    jointNumber: index + 1,
    partDescLeft: '',
    partNoLeft: '',
    heatNumberLeft: '',
    partDescRight: '',
    partNoRight: '',
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

function childParts(nodes) {
  const seen = new Map()
  for (const n of nodes) {
    if (n.level >= 1 && n.item && !seen.has(n.item)) seen.set(n.item, { no: n.item, desc: n.itemDesc ?? '' })
  }
  return [...seen.values()]
}

function newReport(workOrderNumber, summary) {
  const today = todayIso()
  return {
    id: 0,
    workOrderNumber,
    reportRequired: true,
    completedAt: null,
    rowVersion: null,
    dateWelded: today,
    partNo: summary?.assemblyItem ?? '',
    description: summary?.assemblyDesc ?? '',
    materialSpec1: '', materialSpec2: '', materialSpec3: '',
    grade1: '', grade2: '', grade3: '',
    pNumber1: '', pNumber2: '', pNumber3: '',
    joints: [blankJoint(0)],
  }
}

const RECENT_KEY = 'dcenter.report.recent'
const RECENT_MAX = 5

function readRecent() {
  try {
    const list = JSON.parse(localStorage.getItem(RECENT_KEY) ?? '[]')
    return Array.isArray(list) ? list.filter((x) => typeof x === 'string').slice(0, RECENT_MAX) : []
  } catch {
    return []
  }
}

function writeRecent(list) {
  try {
    localStorage.setItem(RECENT_KEY, JSON.stringify(list))
  } catch {
    // storage unavailable; recents stay in memory only
  }
}

function normPNo(v) {
  return (v ?? '').trim().toLowerCase()
}

function blankAutoPNo() {
  return { 1: '', 2: '', 3: '' }
}

const SEARCH_DEBOUNCE_MS = 350
const MIN_QUERY_LENGTH = 2
const PAGE_SIZE = 25
let searchTimer = null
let searchToken = 0
const TREE_MAX_LEVEL = 20
const TREE_MAX_NODES = 5000
const CHILD_CHUNK = 500
const childrenByItem = new Map()
const treeLoads = new Map()

function itemKey(item) {
  return (item ?? '').trim().toUpperCase()
}

function comparePath(a, b) {
  return a.path < b.path ? -1 : a.path > b.path ? 1 : 0
}

async function fetchChildren(items) {
  const missing = [...new Set(items.map((i) => (i ?? '').trim()).filter(Boolean))]
    .filter((i) => !childrenByItem.has(itemKey(i)))
  const chunks = []
  for (let i = 0; i < missing.length; i += CHILD_CHUNK) chunks.push(missing.slice(i, i + CHILD_CHUNK))
  await Promise.all(chunks.map(async (chunk) => {
    const { data } = await api.post('/workorders/bom/children', { items: chunk })
    const grouped = new Map(chunk.map((c) => [itemKey(c), []]))
    for (const link of data ?? []) {
      const key = itemKey(link.item)
      if (!grouped.has(key)) grouped.set(key, [])
      grouped.get(key).push(link)
    }
    for (const [key, links] of grouped) childrenByItem.set(key, links)
  }))
}
let traceToken = 0
let reportToken = 0
let autofillToken = 0
let dashboardToken = 0
let woOptionTimer = null
let woOptionToken = 0

export const useReportStore = defineStore('report', {
  state: () => ({
    searchInput: '',
    searchQuery: '',
    searchResults: [],
    hasMoreResults: false,
    loadingRows: false,
    loadingMoreRows: false,
    confirmed: false,
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
    treeByWorkOrder: {},
    loadingTrees: {},
    truncatedTrees: {},
    mode: 'browse',
    workOrderOptions: [],
    loadingWorkOrderNumbers: false,
    autoPNo: blankAutoPNo(),
    recent: readRecent(),
    listTab: 'all',
    dashboard: null,
    dashboardMonths: 6,
    loadingDashboard: false,
    dashboardError: '',
    traceField: 'welder',
    traceQuery: '',
    traceResults: [],
    traceTruncated: false,
    traceSearched: false,
    loadingTrace: false,
    traceError: '',
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
      const pnos = this.filterPNos.map(normPNo)
      if (!pnos.length) return []
      const seen = new Map()
      for (const w of useWpsStore().items) {
        if (pnos.includes(normPNo(w.pNo)) && !seen.has(w.wpsNo)) {
          seen.set(w.wpsNo, { wpsNo: w.wpsNo, process: w.process, baseMetal: w.baseMetal, pNo: w.pNo })
        }
      }
      return [...seen.values()]
    },
    wpsOptions() {
      if (this.allowedWps.length) return this.allowedWps
      return useWpsStore().items.map((w) => ({ wpsNo: w.wpsNo, process: w.process, baseMetal: w.baseMetal, pNo: w.pNo }))
    },
    wpsFilterNote() {
      const list = this.filterPNos.join(', ')
      if (!this.filterPNos.length) return 'Showing all WPS — fill a P# above to narrow.'
      if (!this.allowedWps.length) return `No WPS for P-No ${list} — showing all.`
      return `Filtered to P-No ${list}.`
    },
    loadingWpsFilter: () => useWpsStore().loading,
    filteredRows: (s) => s.searchResults,
    distinctWorkOrders() {
      return [...new Set(this.filteredRows.map((r) => r.workOrderNumber))]
    },
    exactMatch() {
      const q = this.searchInput.trim().toLowerCase()
      if (!q) return null
      return this.filteredRows.find((r) => r.workOrderNumber.toLowerCase() === q)?.workOrderNumber ?? null
    },
    targetWorkOrder() {
      return this.exactMatch ?? (this.distinctWorkOrders.length === 1 ? this.distinctWorkOrders[0] : null)
    },
    savedByWorkOrder() {
      return new Map(this.savedReports.map((r) => [r.workOrderNumber, r]))
    },
    rightParts() {
      const wo = (this.report?.workOrderNumber ?? '').trim()
      return childParts(this.treeByWorkOrder[wo] ?? this.reportParts)
    },
    isDirty: (s) => !!s.report && JSON.stringify(s.report) !== s.savedSnapshot,
    needsLeavePrompt() {
      return this.confirmed && this.isDirty && !this.isComplete
    },
  },

  actions: {
    async fetchReportFile(workOrderNumber, kind) {
      const { data } = await api.get(`/reports/${encodeURIComponent(workOrderNumber)}/${kind}`, { responseType: 'blob' })
      return data
    },

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
        this.error = errorText(e, 'Could not load work orders.')
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

    loadTree(workOrderNumber) {
      const wo = (workOrderNumber ?? '').trim()
      if (!wo) return Promise.resolve([])
      if (treeLoads.has(wo)) return treeLoads.get(wo)
      if (this.treeByWorkOrder[wo]) return Promise.resolve(this.treeByWorkOrder[wo])
      const run = this.walkTree(wo).finally(() => treeLoads.delete(wo))
      treeLoads.set(wo, run)
      return run
    },

    async walkTree(wo) {
      this.loadingTrees[wo] = { level: 0 }
      delete this.truncatedTrees[wo]
      try {
        const summary = this.searchResults.find((r) => r.workOrderNumber === wo) ?? await this.fetchHeader(wo)
        if (!summary) {
          this.treeByWorkOrder[wo] = []
          return this.treeByWorkOrder[wo]
        }
        const root = (summary.assemblyItem ?? '').trim()
        this.treeByWorkOrder[wo] = [{ level: 0, parentItem: null, item: root, itemDesc: summary.assemblyDesc ?? '', path: `/${root}/` }]
        const nodes = this.treeByWorkOrder[wo]
        let frontier = root ? [nodes[0]] : []
        for (let level = 1; level <= TREE_MAX_LEVEL && frontier.length; level++) {
          this.loadingTrees[wo] = { level }
          await fetchChildren(frontier.map((n) => n.item))
          const next = []
          for (const parent of frontier) {
            for (const link of childrenByItem.get(itemKey(parent.item)) ?? []) {
              if (parent.path.includes(`/${link.component}/`)) continue
              next.push({
                level,
                parentItem: parent.item,
                item: link.component,
                itemDesc: link.componentDesc ?? '',
                path: `${parent.path}${link.component}/`,
              })
            }
          }
          const room = TREE_MAX_NODES - nodes.length
          if (next.length > room) {
            nodes.push(...next.slice(0, room))
            nodes.sort(comparePath)
            this.truncatedTrees[wo] = true
            break
          }
          nodes.push(...next)
          nodes.sort(comparePath)
          frontier = next
        }
        return nodes
      } catch (e) {
        delete this.treeByWorkOrder[wo]
        throw e
      } finally {
        delete this.loadingTrees[wo]
      }
    },

    async loadReportParts(workOrderNumber) {
      const wo = (workOrderNumber ?? '').trim()
      if (!wo) { this.reportParts = []; return }
      try {
        const tree = await this.loadTree(wo)
        if (this.report?.workOrderNumber === wo) this.reportParts = tree
      } catch {
        if (this.report?.workOrderNumber === wo) this.reportParts = []
      }
    },

    searchWorkOrderNumbers(term) {
      const q = (term ?? '').trim()
      clearTimeout(woOptionTimer)
      if (q.length < MIN_QUERY_LENGTH) {
        this.workOrderOptions = []
        this.loadingWorkOrderNumbers = false
        return
      }
      this.loadingWorkOrderNumbers = true
      woOptionTimer = setTimeout(async () => {
        const token = ++woOptionToken
        try {
          const { data } = await api.get('/workorders/search', { params: { q, skip: 0, take: PAGE_SIZE } })
          if (token === woOptionToken) this.workOrderOptions = (data?.items ?? []).map((r) => r.workOrderNumber)
        } catch {
          // the field still accepts a typed work order number
        } finally {
          if (token === woOptionToken) this.loadingWorkOrderNumbers = false
        }
      }, SEARCH_DEBOUNCE_MS)
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

    // null when the work order does not exist; connection or server errors are thrown so they are not
    // reported to the user as "not found".
    async fetchHeader(workOrderNumber) {
      const { data } = await api.get(`/workorders/${encodeURIComponent(workOrderNumber)}/header`)
      return data && typeof data === 'object'
        ? { assemblyItem: data.partNo, assemblyDesc: data.description }
        : null
    },

    async loadForWorkOrder(workOrderNumber, { force = false } = {}) {
      const wo = (workOrderNumber ?? '').trim()
      if (!wo) return false
      if (!force && this.confirmed && this.report?.workOrderNumber === wo) return true
      const token = ++reportToken
      this.loading = true
      this.error = ''
      this.conflict = false
      this.history = []
      try {
        const res = await api.get(`/reports/${encodeURIComponent(wo)}`)
        if (token !== reportToken) return false
        const isNew = res.status === 204 || !res.data
        if (isNew) {
          const summary = this.searchResults.find((r) => r.workOrderNumber === wo) ?? await this.fetchHeader(wo)
          if (token !== reportToken) return false
          if (!summary) {
            this.backToList()
            this.error = `Work order ${wo} was not found.`
            return false
          }
          this.report = newReport(wo, summary)
        } else {
          this.report = normalizeJoints(res.data)
        }
        this.mode = isNew ? 'new' : 'saved'
        this.confirmed = true
        this.reportParts = []
        this.autoPNo = blankAutoPNo()
        this.markPristine()
        this.ensureRefData()
        this.rememberRecent(wo)
        this.loadTree(wo).catch(() => {})
        return true
      } catch {
        if (token !== reportToken) return false
        this.backToList()
        this.error = `Could not open the report for ${wo}.`
        return false
      } finally {
        if (token === reportToken) this.loading = false
      }
    },

    async reloadReport() {
      if (!this.report?.workOrderNumber) return false
      return this.loadForWorkOrder(this.report.workOrderNumber, { force: true })
    },

    rememberRecent(workOrderNumber) {
      this.recent = [workOrderNumber, ...this.recent.filter((w) => w !== workOrderNumber)].slice(0, RECENT_MAX)
      writeRecent(this.recent)
    },

    forgetRecent(workOrderNumber) {
      this.recent = this.recent.filter((w) => w !== workOrderNumber)
      writeRecent(this.recent)
    },

    syncPNumbers({ overwrite = false } = {}) {
      const r = this.report
      const bpvc = useBpvcStore()
      if (!r || this.isComplete || !bpvc.loaded) return
      for (const c of [1, 2, 3]) {
        const key = `pNumber${c}`
        const pno = bpvc.resolvePNo(r[`materialSpec${c}`], r[`grade${c}`])
        const current = (r[key] ?? '').trim()
        const lastAuto = this.autoPNo[c]
        if (pno) {
          if (overwrite || !current || current === lastAuto) {
            r[key] = pno
            this.autoPNo[c] = pno
          }
        } else if (lastAuto && current === lastAuto) {
          r[key] = ''
          this.autoPNo[c] = ''
        }
      }
    },

    async save() {
      if (!this.report?.dateWelded) {
        this.error = 'Date welded is required before saving.'
        return false
      }
      if (this.isComplete) {
        this.error = 'This report is completed. Ask a supervisor to reopen it before making changes.'
        return false
      }
      const wo = (this.report.workOrderNumber ?? '').trim()
      if (this.mode === 'duplicate' && this.savedByWorkOrder.has(wo)) {
        this.error = `A report for work order ${wo} already exists. Choose another work order, or open the existing report from Saved reports.`
        return false
      }
      this.saving = true
      this.error = ''
      this.conflict = false
      try {
        const { data } = await api.post('/reports', this.report)
        this.report = normalizeJoints(data)
        this.mode = 'saved'
        this.markPristine()
        this.rememberRecent(this.report.workOrderNumber)
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.conflict = e.response?.status === 409
        this.error = errorText(e, 'Save failed.')
        return false
      } finally {
        this.saving = false
      }
    },

    async setComplete(complete) {
      if (!this.report?.workOrderNumber) return false
      this.error = ''
      try {
        const wo = this.report.workOrderNumber
        await api.post(`/reports/${encodeURIComponent(wo)}/complete`, complete)
        const { data } = await api.get(`/reports/${encodeURIComponent(wo)}`)
        if (this.report?.workOrderNumber === wo && data) {
          this.report.rowVersion = data.rowVersion
          this.report.completedAt = data.completedAt
        }
        this.markPristine()
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.error = errorText(e, 'Could not update status.')
        return false
      }
    },

    ensureRefData() {
      useBpvcStore().load()
      useWpsStore().load()
    },

    setJointCount(n) {
      const count = Math.min(MAX_JOINTS, Math.max(1, Math.round(n) || 1))
      const joints = this.report.joints
      if (joints.length > count) joints.length = count
      else while (joints.length < count) joints.push(blankJoint(joints.length))
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
        const wo = this.report.workOrderNumber
        await api.delete(`/reports/${encodeURIComponent(wo)}`)
        this.forgetRecent(wo)
        this.markPristine()
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.error = errorText(e, 'Could not delete the draft.')
        return false
      } finally {
        this.deleting = false
      }
    },

    backToList() {
      this.confirmed = false
      this.report = null
      this.history = []
      this.conflict = false
      this.savedSnapshot = ''
      this.reportParts = []
      this.autoPNo = blankAutoPNo()
      this.mode = 'browse'
    },

    async deleteSaved(workOrderNumber) {
      this.error = ''
      try {
        await api.delete(`/reports/${encodeURIComponent(workOrderNumber)}`)
        this.forgetRecent(workOrderNumber)
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.error = errorText(e, 'Could not delete the draft.')
        return false
      }
    },

    async duplicateReport(workOrderNumber) {
      const token = ++reportToken
      this.loading = true
      this.error = ''
      try {
        const res = await api.get(`/reports/${encodeURIComponent(workOrderNumber)}`)
        if (token !== reportToken) return false
        if (res.status === 204 || !res.data) {
          this.error = 'That report no longer exists.'
          return false
        }
        const src = normalizeJoints(res.data)
        const today = todayIso()
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
        this.mode = 'duplicate'
        this.confirmed = true
        this.history = []
        this.conflict = false
        this.reportParts = []
        this.autoPNo = blankAutoPNo()
        this.markPristine()
        this.ensureRefData()
        return true
      } catch {
        if (token === reportToken) this.error = 'Could not duplicate the report.'
        return false
      } finally {
        if (token === reportToken) this.loading = false
      }
    },

    async autofillFromWorkOrder(workOrderNumber) {
      if (!this.report || this.report.id !== 0) return
      const wo = (workOrderNumber ?? '').trim()
      this.report.partNo = ''
      this.report.description = ''
      this.error = this.savedByWorkOrder.has(wo)
        ? `Work order ${wo} already has a saved report. Choose another work order, or open the existing one from Saved reports.`
        : ''
      if (!wo) { this.reportParts = []; return }
      this.loadReportParts(wo)
      const token = ++autofillToken
      const report = this.report
      try {
        const { data } = await api.get(`/workorders/${encodeURIComponent(wo)}/header`)
        if (token !== autofillToken || this.report !== report) return
        if (data && typeof data === 'object') {
          this.report.partNo = data.partNo ?? ''
          this.report.description = data.description ?? ''
        }
      } catch {
        // no matching work order header in the ERP source; fields stay cleared
      }
    },

    async loadDashboard(months = this.dashboardMonths) {
      const token = ++dashboardToken
      this.dashboardMonths = months
      this.loadingDashboard = true
      this.dashboardError = ''
      try {
        const { data } = await api.get('/reports/dashboard', { params: { months } })
        if (token === dashboardToken) this.dashboard = data
      } catch {
        if (token === dashboardToken) this.dashboardError = 'Could not load the report dashboard.'
      } finally {
        if (token === dashboardToken) this.loadingDashboard = false
      }
    },

    async searchTrace() {
      const q = this.traceQuery.trim()
      if (q.length < MIN_QUERY_LENGTH) {
        this.traceResults = []
        this.traceTruncated = false
        this.traceSearched = false
        return
      }
      const token = ++traceToken
      this.loadingTrace = true
      this.traceError = ''
      try {
        const { data } = await api.get('/reports/trace', { params: { field: this.traceField, q } })
        if (token !== traceToken) return
        this.traceResults = data.items ?? []
        this.traceTruncated = !!data.truncated
        this.traceSearched = true
      } catch (e) {
        if (token !== traceToken) return
        this.traceError = typeof e.response?.data === 'string' && e.response.data ? e.response.data : 'Search failed.'
        this.traceResults = []
      } finally {
        if (token === traceToken) this.loadingTrace = false
      }
    },

    markPristine() {
      this.savedSnapshot = this.report ? JSON.stringify(this.report) : ''
    },
  },
})

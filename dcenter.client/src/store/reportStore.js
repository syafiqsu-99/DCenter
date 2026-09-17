import { defineStore } from 'pinia'
import api from '@/utils/api'

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

function newReport(jobNumber, rows) {
  const first = rows[0] ?? {}
  const today = new Date().toISOString().slice(0, 10)
  return {
    id: 0,
    jobNumber,
    reportRequired: true,
    completedAt: null,
    rowVersion: null,
    dateWelded: today,
    workOrder: jobNumber,
    partNo: first.assemblyItem ?? '',
    description: first.itemDesc ?? '',
    materialSpec1: '', materialSpec2: '', materialSpec3: '',
    grade1: '', grade2: '', grade3: '',
    pNumber1: '', pNumber2: '', pNumber3: '',
    joints: rows.slice(0, 9).map((row, i) => jointFromRow(i, row)),
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
    resolvedJob: '',
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
    wpsFilter: null,
    loadingWpsFilter: false,
  }),

  getters: {
    isComplete: (s) => !!s.report?.completedAt,
    hasDateWelded: (s) => !!s.report?.dateWelded,
    jointCount: (s) => s.report?.joints.length ?? 0,
    allowedWps: (s) => s.wpsFilter?.wps ?? [],
    wpsFilterNote: (s) => s.wpsFilter?.note ?? null,
    filteredRows: (s) => s.searchResults,
    distinctJobs() {
      return [...new Set(this.filteredRows.map((r) => r.jobNumber))]
    },
    canSelect() {
      return this.distinctJobs.length === 1
    },
    selectedRows() {
      return this.searchResults.filter((r) => r.jobNumber === this.resolvedJob)
    },
    savedByJobNumber() {
      return new Map(this.savedReports.map((r) => [r.jobNumber, r]))
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
      for (const r of this.selectedRows) {
        if (r.assemblyItem && !seen.has(r.assemblyItem)) {
          seen.set(r.assemblyItem, { no: r.assemblyItem, desc: r.itemDesc ?? '' })
        }
      }
      return [...seen.values()]
    },
    rightParts() {
      const seen = new Map()
      for (const r of this.selectedRows) {
        if (r.childPart && !seen.has(r.childPart)) {
          seen.set(r.childPart, { no: r.childPart, desc: r.componentDesc ?? '' })
        }
      }
      return [...seen.values()]
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
        const { data } = await api.get('/jobs/search', {
          params: { q: this.searchQuery, skip: 0, take: PAGE_SIZE },
        })
        if (token !== searchToken) return
        this.searchResults = data.items.map((r, i) => ({ ...r, _index: i }))
        this.hasMoreResults = data.hasMore
      } catch (e) {
        if (token !== searchToken) return
        this.error = e.response?.data ?? 'Could not load jobs.'
        this.searchResults = []
        this.hasMoreResults = false
      } finally {
        if (token === searchToken) this.loadingRows = false
      }
    },

    async loadMoreJobs() {
      if (this.loadingRows || this.loadingMoreRows || !this.hasMoreResults) return
      const token = searchToken
      this.loadingMoreRows = true
      try {
        const { data } = await api.get('/jobs/search', {
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

    async selectJob() {
      if (!this.canSelect) return
      this.resolvedJob = this.distinctJobs[0]
      this.loading = true
      this.error = ''
      try {
        const res = await api.get(`/reports/${encodeURIComponent(this.resolvedJob)}`)
        this.report =
          res.status === 204 || !res.data
            ? newReport(this.resolvedJob, this.selectedRows)
            : normalizeJoints(res.data)
        this.confirmed = true
        this.loadWpsFilter(this.resolvedJob)
      } catch {
        this.error = 'Could not load the report draft.'
      } finally {
        this.loading = false
      }
    },

    async openReport(jobNumber) {
      this.resolvedJob = jobNumber
      this.loading = true
      this.error = ''
      try {
        const res = await api.get(`/reports/${encodeURIComponent(jobNumber)}`)
        if (res.status === 204 || !res.data) {
          this.error = 'That report no longer exists.'
          return
        }
        this.report = normalizeJoints(res.data)
        this.confirmed = true
        this.loadWpsFilter(jobNumber)
      } catch {
        this.error = 'Could not open the report.'
      } finally {
        this.loading = false
      }
    },

    async reloadReport() {
      if (!this.report?.jobNumber) return false
      return this.openReport(this.report.jobNumber)
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
      if (!this.report?.jobNumber) return false
      this.error = ''
      try {
        await api.post(`/reports/${encodeURIComponent(this.report.jobNumber)}/complete`, complete)
        this.report.completedAt = complete ? new Date().toISOString() : null
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.error = e.response?.data ?? 'Could not update status.'
        return false
      }
    },

    async loadWpsFilter(jobNumber) {
      this.loadingWpsFilter = true
      try {
        const { data } = await api.get(`/weldreference/wps-for-job/${encodeURIComponent(jobNumber)}`)
        this.wpsFilter = data
        if (this.report?.id === 0 && data.wps.length) {
          this.setJointCount(data.wps.length)
        }
      } catch {
        this.wpsFilter = null
      } finally {
        this.loadingWpsFilter = false
      }
    },

    setJointCount(n) {
      const count = Math.min(9, Math.max(1, Math.round(n) || 1))
      const joints = this.report.joints
      if (joints.length > count) joints.length = count
      else while (joints.length < count) joints.push(jointFromRow(joints.length, this.selectedRows[joints.length]))
    },

    async loadHistory() {
      if (!this.report?.jobNumber) return
      this.loadingHistory = true
      try {
        const { data } = await api.get(`/reports/${encodeURIComponent(this.report.jobNumber)}/history`)
        this.history = data
      } catch {
        this.history = []
      } finally {
        this.loadingHistory = false
      }
    },
    
    async deleteDraft() {
      if (!this.report?.jobNumber) return false
      this.deleting = true
      this.error = ''
      try {
        await api.delete(`/reports/${encodeURIComponent(this.report.jobNumber)}`)
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
      this.resolvedJob = ''
      this.history = []
      this.conflict = false
      this.wpsFilter = null
    },

    async deleteSaved(jobNumber) {
      this.error = ''
      try {
        await api.delete(`/reports/${encodeURIComponent(jobNumber)}`)
        await this.loadSavedReports()
        return true
      } catch (e) {
        this.error = e.response?.data ?? 'Could not delete the draft.'
        return false
      }
    },
  },
})

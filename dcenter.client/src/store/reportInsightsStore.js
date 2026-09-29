import { defineStore } from 'pinia'
import api from '@/utils/api'

const MIN_QUERY_LENGTH = 2
let traceToken = 0
let dashboardToken = 0

// Read-only views across all reports: the dashboard and the welder / WPS / heat trace.
export const useReportInsightsStore = defineStore('reportInsights', {
  state: () => ({
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

  actions: {
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
  },
})

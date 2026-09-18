import { defineStore } from 'pinia'
import api from '@/utils/api'

function norm(v) {
  return (v ?? '').trim().toLowerCase()
}

export const useBpvcStore = defineStore('bpvc', {
  state: () => ({
    items: [],
    loaded: false,
    loading: false,
  }),

  getters: {
    materialOptions: (s) => [...new Set(s.items.map((b) => b.specNoRaw).filter(Boolean))].sort(),
    gradeOptions: (s) => [...new Set(s.items.map((b) => b.designation).filter(Boolean))].sort(),
  },

  actions: {
    async load(force = false) {
      if ((this.loaded || this.loading) && !force) return
      this.loading = true
      try {
        const { data } = await api.get('/bpvc')
        this.items = data
        this.loaded = true
      } catch {
        this.items = []
      } finally {
        this.loading = false
      }
    },

    resolvePNo(specNoRaw, designation) {
      const m = norm(specNoRaw)
      const g = norm(designation)
      if (!m || !g) return ''
      const pnos = [
        ...new Set(
          this.items
            .filter((b) => norm(b.specNoRaw) === m && norm(b.designation) === g)
            .map((b) => b.pNo)
            .filter(Boolean),
        ),
      ]
      return pnos[0] ?? ''
    },
  },
})

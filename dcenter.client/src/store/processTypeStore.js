import { defineStore } from 'pinia'
import api from '@/utils/api'

function norm(v) {
  return (v ?? '').trim().toLowerCase()
}

export const useProcessTypeStore = defineStore('processType', {
  state: () => ({
    links: [],
    loaded: false,
    loading: false,
  }),

  actions: {
    async load(force = false) {
      if ((this.loaded || this.loading) && !force) return
      this.loading = true
      try {
        const { data } = await api.get('/processtypelinks')
        this.links = data
        this.loaded = true
      } catch {
        this.links = []
      } finally {
        this.loading = false
      }
    },

    typesForProcess(process) {
      const p = norm(process)
      if (!p) return null
      const types = this.links.filter((l) => norm(l.process) === p).map((l) => l.type)
      return types.length ? types : null
    },
  },
})

import { defineStore } from 'pinia'
import api from '@/utils/api'

export const useWpsStore = defineStore('wps', {
  state: () => ({
    items: [],
    loaded: false,
    loading: false,
  }),

  actions: {
    async load(force = false) {
      if ((this.loaded || this.loading) && !force) return
      this.loading = true
      try {
        const { data } = await api.get('/wps')
        this.items = data
        this.loaded = true
      } catch {
        this.items = []
      } finally {
        this.loading = false
      }
    },
  },
})

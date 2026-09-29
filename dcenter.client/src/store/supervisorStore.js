import { defineStore } from 'pinia'
import api from '@/utils/api'
import { SUPERVISOR_HEADER } from '@/utils/constants'

const SUPERVISOR_KEY = 'dcenter.consumables.supervisor'
const SUPERVISOR_RENEW_MS = 60 * 60 * 1000
let refreshing = false

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

// The supervisor login held by this browser tab. Side effects on consumable state (clearing the
// counter welder and dashboard) stay in consumableStore, which wraps these actions.
export const useSupervisorStore = defineStore('supervisor', {
  state: () => ({
    supervisor: null,
    clock: Date.now(),
    reloginPrompt: false,
  }),

  getters: {
    isSupervisor: (s) => !!s.supervisor && Date.parse(s.supervisor.expiresAt) > s.clock,
    name: (s) => s.supervisor?.name ?? '',
  },

  actions: {
    restore() {
      this.supervisor = readSession()
      this.clock = Date.now()
      applySupervisorToken(this.supervisor?.token)
    },

    tick() {
      this.clock = Date.now()
    },

    setSession(data) {
      this.supervisor = { name: data.name, token: data.token, expiresAt: data.expiresAt }
      this.clock = Date.now()
      writeSession(this.supervisor)
      applySupervisorToken(data.token)
    },

    clear() {
      this.supervisor = null
      writeSession(null)
      applySupervisorToken(null)
    },

    async login(name, password) {
      const { data } = await api.post('/supervisor/login', { name, password })
      this.setSession(data)
      return this.supervisor
    },

    async logout() {
      if (this.supervisor) await api.post('/supervisor/logout').catch(() => {})
    },

    async refreshIfNeeded() {
      if (!this.supervisor || refreshing) return
      if (Date.parse(this.supervisor.expiresAt) - Date.now() > SUPERVISOR_RENEW_MS) return
      refreshing = true
      try {
        const { data } = await api.post('/supervisor/session/refresh')
        if (!this.supervisor || !data?.token) return
        this.setSession(data)
      } catch {
        // 401 locks the session through onUnauthorized; network errors retry on the next check
      } finally {
        refreshing = false
      }
    },

    async verify() {
      if (!this.supervisor) return false
      try {
        await api.get('/supervisor/session')
        return true
      } catch {
        return false
      }
    },

    async loadPasswordStatus() {
      const { data } = await api.get('/supervisor/password')
      return data
    },

    async changePassword(currentPassword, newPassword) {
      const { data } = await api.post('/supervisor/password', { currentPassword, newPassword })
      this.reloginPrompt = true
      return data
    },
  },
})

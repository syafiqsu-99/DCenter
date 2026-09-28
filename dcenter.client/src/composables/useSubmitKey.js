import { ref } from 'vue'

function newToken() {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`
}

// One token per form: a retried or resubmitted save reuses it, so the server records it once.
// Renew after a confirmed success so the next, deliberate entry is treated as new.
export function useSubmitKey() {
  const key = ref(newToken())
  function renew() {
    key.value = newToken()
  }
  return { key, renew }
}

import { onBeforeUnmount, onMounted } from 'vue'

// Keeps shop-floor screens current: reloads every interval while the page is visible,
// and again when the tab comes back into view. Skips a round while paused() is true (e.g. a dialog is open).
export function useAutoRefresh(load, { intervalMs = 60000, paused = () => false } = {}) {
  let timer = null
  let running = false

  async function tick() {
    if (running || document.visibilityState !== 'visible' || paused()) return
    running = true
    try {
      await load()
    } catch {
      // the next round retries
    } finally {
      running = false
    }
  }

  function onVisible() {
    if (document.visibilityState === 'visible') tick()
  }

  onMounted(() => {
    timer = setInterval(tick, intervalMs)
    document.addEventListener('visibilitychange', onVisible)
  })

  onBeforeUnmount(() => {
    clearInterval(timer)
    document.removeEventListener('visibilitychange', onVisible)
  })
}

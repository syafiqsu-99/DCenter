import { ref } from 'vue'
import { useReportStore } from '@/store/reportStore'

const leaveDialog = ref(false)
let onProceed = null

export function useLeaveGuard() {
  const store = useReportStore()

  function hold(proceed) {
    onProceed = proceed
    leaveDialog.value = true
  }

  function guardLeave(proceed) {
    if (store.needsLeavePrompt) {
      hold(proceed)
      return false
    }
    proceed()
    return true
  }

  function stay() {
    onProceed = null
    leaveDialog.value = false
  }

  function discardAndProceed() {
    const p = onProceed
    onProceed = null
    leaveDialog.value = false
    p?.()
  }

  async function saveAndProceed(complete) {
    if (!(await store.save())) return
    if (complete && !(await store.setComplete(true))) return
    const p = onProceed
    onProceed = null
    leaveDialog.value = false
    p?.()
  }

  return { leaveDialog, hold, guardLeave, stay, discardAndProceed, saveAndProceed }
}

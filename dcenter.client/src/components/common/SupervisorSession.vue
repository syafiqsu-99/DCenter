<template>
  <SupervisorUnlockDialog v-model="open" @unlocked="onUnlocked" @update:model-value="onDialogToggle" />
  <v-snackbar v-model="notice" :color="noticeColor" timeout="5000">{{ noticeText }}</v-snackbar>
</template>

<script setup>
  import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import SupervisorUnlockDialog from '@/components/common/SupervisorUnlockDialog.vue'

  const SUPERVISOR_IDLE_MS = 15 * 60 * 1000
  const WELDER_IDLE_MS = 5 * 60 * 1000
  const ACTIVITY_EVENTS = ['pointerdown', 'keydown']

  const route = useRoute()
  const router = useRouter()
  const store = useConsumableStore()
  const open = ref(false)
  const notice = ref(false)
  const noticeText = ref('')
  const noticeColor = ref('info')
  let lastActivity = Date.now()
  let timer = null
  let idleLogout = false

  const onSupervisorPage = () => route.matched.some((r) => r.meta.supervisor)

  function withoutUnlockQuery() {
    const { unlock, next, ...rest } = route.query
    return { path: route.path, query: rest }
  }

  function show(color, text) {
    noticeColor.value = color
    noticeText.value = text
    notice.value = true
  }

  function onUnlocked() {
    const next = typeof route.query.next === 'string' && route.query.next.startsWith('/') ? route.query.next : null
    if (next) router.replace(next)
    else if (route.name === 'consumable-welder') router.replace({ name: 'consumable-dashboard' })
    else if (route.query.unlock) router.replace(withoutUnlockQuery())
    show('success', `Logged in as ${store.supervisor.name}. Settings and the supervisor view are now available.`)
  }

  function onDialogToggle(value) {
    if (!value && route.query.unlock && !store.isSupervisor) router.replace(withoutUnlockQuery())
  }

  function touch() {
    lastActivity = Date.now()
  }

  function checkIdle() {
    const idle = Date.now() - lastActivity
    if (store.supervisor && idle > SUPERVISOR_IDLE_MS) {
      idleLogout = true
      store.lockSupervisor()
    }
    store.tick()
    if (!store.isSupervisor && store.counterWelder && idle > WELDER_IDLE_MS) store.setWelder(null)
  }

  watch(() => store.isSupervisor, (supervisor) => {
    if (supervisor) return
    const relogin = store.reloginPrompt
    store.reloginPrompt = false
    if (relogin) show('success', 'Supervisor password changed. Log in again with the new password.')
    else show('info', idleLogout ? 'Logged out after 15 minutes of inactivity.' : 'Logged out.')
    idleLogout = false
    if (onSupervisorPage()) router.push({ name: 'consumable-welder', query: relogin ? { unlock: '1' } : {} })
  })

  watch(() => route.query.unlock, (value) => {
    if (value === '1' && !store.isSupervisor) open.value = true
  }, { immediate: true })

  onMounted(async () => {
    if (!store.supervisor) store.init()
    if (store.supervisor && !(await store.verifySupervisor()) && onSupervisorPage()) router.push({ name: 'consumable-welder' })
    ACTIVITY_EVENTS.forEach((e) => window.addEventListener(e, touch, { passive: true }))
    timer = setInterval(checkIdle, 30000)
  })

  onBeforeUnmount(() => {
    ACTIVITY_EVENTS.forEach((e) => window.removeEventListener(e, touch))
    clearInterval(timer)
  })
</script>

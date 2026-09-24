<template>
  <v-btn v-if="!store.isSupervisor" variant="outlined" prepend-icon="mdi-shield-lock-outline" class="me-3" @click="open = true">
    Login
  </v-btn>
  <v-menu v-else location="bottom end">
    <template #activator="{ props: menu }">
      <v-btn v-bind="menu" variant="tonal" prepend-icon="mdi-shield-account" append-icon="mdi-chevron-down" class="me-3">
        {{ store.supervisor.name }}
      </v-btn>
    </template>
    <v-list density="comfortable" min-width="220">
      <v-list-item prepend-icon="mdi-key-change" title="Change password" @click="router.push({ name: 'settings', query: { section: 'supervisor' } })" />
      <v-divider />
      <v-list-item prepend-icon="mdi-logout" title="Logout" base-color="error" @click="logout" />
    </v-list>
  </v-menu>

  <SupervisorUnlockDialog v-model="open" @unlocked="onUnlocked" @update:model-value="onDialogToggle" />
  <v-snackbar v-model="notice" :color="noticeColor" timeout="5000">{{ noticeText }}</v-snackbar>
</template>

<script setup>
  import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
  import { useRoute, useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import SupervisorUnlockDialog from '@/components/consumables/SupervisorUnlockDialog.vue'

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

  const onSupervisorPage = () => route.matched.some((r) => r.meta.supervisor)

  function withoutUnlockQuery() {
    const { unlock, next, ...rest } = route.query
    return { path: route.path, query: rest }
  }

  function onUnlocked() {
    const next = typeof route.query.next === 'string' && route.query.next.startsWith('/') ? route.query.next : null
    if (next) router.replace(next)
    else if (route.name === 'consumable-welder') router.replace({ name: 'consumable-dashboard' })
    else if (route.query.unlock) router.replace(withoutUnlockQuery())
    noticeColor.value = 'success'
    noticeText.value = `Logged in as ${store.supervisor.name}. Settings and the supervisor view are now available.`
    notice.value = true
  }

  function onDialogToggle(value) {
    if (!value && route.query.unlock && !store.isSupervisor) router.replace(withoutUnlockQuery())
  }

  function logout(reason) {
    store.lockSupervisor()
    if (onSupervisorPage()) router.push({ name: 'consumable-welder' })
    noticeColor.value = 'info'
    noticeText.value = reason === 'idle' ? 'Logged out after 15 minutes of inactivity.' : 'Logged out.'
    notice.value = true
  }

  function touch() {
    lastActivity = Date.now()
  }

  function checkIdle() {
    const idle = Date.now() - lastActivity
    if (store.supervisor && (!store.isSupervisor || idle > SUPERVISOR_IDLE_MS)) logout('idle')
    if (!store.isSupervisor && store.counterWelder && idle > WELDER_IDLE_MS) store.setWelder(null)
  }

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

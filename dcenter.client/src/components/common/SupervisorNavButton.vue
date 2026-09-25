<template>
  <v-btn v-if="!store.isSupervisor" variant="outlined" prepend-icon="mdi-shield-lock-outline" class="me-3" @click="login">
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
      <v-list-item prepend-icon="mdi-logout" title="Logout" base-color="error" @click="store.lockSupervisor()" />
    </v-list>
  </v-menu>
</template>

<script setup>
  import { useRoute, useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'

  const route = useRoute()
  const router = useRouter()
  const store = useConsumableStore()

  function login() {
    router.replace({ path: route.path, query: { ...route.query, unlock: '1' } })
  }
</script>

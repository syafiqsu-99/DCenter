<template>
  <nav class="modules" aria-label="Modules">
    <ModuleCard title="Report" icon="mdi-file-document-edit-outline" :index="0" @open="router.push('/report')" />
    <ModuleCard title="Consumables" icon="mdi-package-variant-closed" :index="1"
                @open="router.push(store.isSupervisor ? '/consumables/dashboard' : '/consumables/station')" />
    <ModuleCard title="Settings" icon="mdi-cog-outline" :index="2" :locked="!store.isSupervisor"
                @open="router.push('/settings')" />
  </nav>
</template>

<script setup>
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import ModuleCard from '@/components/home/ModuleCard.vue'

  const router = useRouter()
  const store = useConsumableStore()
</script>

<style scoped>
  .modules {
    display: flex;
    flex-wrap: wrap;
    justify-content: center;
    gap: 20px;
  }

  @media (max-width: 600px) {
    .modules :deep(.module-card) {
      width: 130px;
      height: 120px;
    }
  }
</style>

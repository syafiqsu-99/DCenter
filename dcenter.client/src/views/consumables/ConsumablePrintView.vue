<template>
  <component :is="printComponent" v-if="printComponent" :query="route.query" />
  <v-alert v-else type="error" variant="tonal" class="ma-4">Unknown print page.</v-alert>
</template>

<script setup>
  import { computed } from 'vue'
  import { useRoute } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import BakingRecordsPrint from '@/components/consumables/print/BakingRecordsPrint.vue'
  import HoldingRecordsPrint from '@/components/consumables/print/HoldingRecordsPrint.vue'
  import CountSheetPrint from '@/components/consumables/print/CountSheetPrint.vue'
  import CountResultPrint from '@/components/consumables/print/CountResultPrint.vue'

  const route = useRoute()
  useConsumableStore().init()

  const pages = {
    'baking-records': BakingRecordsPrint,
    'holding-records': HoldingRecordsPrint,
    'count-sheet': CountSheetPrint,
    'count-result': CountResultPrint,
  }
  const printComponent = computed(() => pages[route.params.kind] ?? null)
</script>

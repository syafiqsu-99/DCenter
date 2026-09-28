<template>
  <div class="py-2 px-4">
    <div v-if="progress" class="d-flex align-center ga-2 text-caption text-medium-emphasis mb-1">
      <v-progress-circular indeterminate size="14" width="2" color="primary" />
      {{ progress.level ? `Loading BOM level ${progress.level}…` : 'Loading work order…' }}
      <span v-if="nodes.length > 1">{{ nodes.length - 1 }} part(s) so far</span>
    </div>
    <div v-if="error" class="text-error text-body-2 py-2">{{ error }}</div>
    <div v-else-if="!progress && !nodes.length" class="text-medium-emphasis text-body-2 py-2">No BOM found for this work order.</div>
    <v-data-table-virtual v-if="nodes.length" :headers="headers" :items="nodes" item-value="path"
                          density="compact" fixed-header class="bg-transparent tree-table virtual-capped">
      <template #item.level="{ item }">
        <v-chip size="x-small" label :color="item.level === 0 ? 'primary' : undefined"
                :variant="item.level === 0 ? 'flat' : 'tonal'">
          {{ item.level === 0 ? 'Assembly' : `L${item.level}` }}
        </v-chip>
      </template>
      <template #item.item="{ item }">
        <span class="tree-cell" :style="{ paddingLeft: `${item.level * 16}px` }">
          <span v-if="item.level > 0" class="tree-branch">└</span>
          <span :class="item.level === 0 ? 'font-weight-bold' : ''">{{ item.item || '—' }}</span>
        </span>
      </template>
      <template #item.itemDesc="{ item }">{{ item.itemDesc || '—' }}</template>
    </v-data-table-virtual>
    <div v-if="truncated" class="text-caption text-warning mt-1">
      Showing the first 5,000 parts of this BOM.
    </div>
  </div>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useReportStore } from '@/store/reportStore'

  const props = defineProps({ workOrderNumber: { type: String, required: true } })
  const store = useReportStore()
  const error = ref('')

  const headers = [
    { title: 'Level', key: 'level', width: '100px', sortable: false },
    { title: 'Item', key: 'item', sortable: false },
    { title: 'Description', key: 'itemDesc', sortable: false },
  ]

  const nodes = computed(() => store.treeByWorkOrder[props.workOrderNumber] ?? [])
  const progress = computed(() => store.loadingTrees[props.workOrderNumber] ?? null)
  const truncated = computed(() => !!store.truncatedTrees[props.workOrderNumber])

  onMounted(async () => {
    try {
      await store.loadTree(props.workOrderNumber)
    } catch (e) {
      error.value = typeof e.response?.data === 'string' && e.response.data ? e.response.data : 'Could not load the BOM for this work order.'
    }
  })
</script>

<style scoped>
  .tree-cell {
    display: inline-flex;
    align-items: center;
    gap: 6px;
  }

  .tree-branch {
    color: rgba(0, 0, 0, 0.38);
  }

  .tree-table :deep(td),
  .tree-table :deep(th) {
    white-space: normal;
  }
</style>

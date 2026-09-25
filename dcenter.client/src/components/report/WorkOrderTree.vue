<template>
  <div class="py-2 px-4">
    <v-skeleton-loader v-if="loading" type="table-row@3" class="bg-transparent" />
    <div v-else-if="error" class="text-error text-body-2 py-2">{{ error }}</div>
    <div v-else-if="!nodes.length" class="text-medium-emphasis text-body-2 py-2">No BOM found for this work order.</div>
    <v-table v-else density="compact" class="bg-transparent tree-table">
      <thead>
        <tr>
          <th style="width:100px">Level</th>
          <th>Item</th>
          <th>Description</th>
          <th style="width:140px">MRN</th>
          <th>MRN Description</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="n in nodes" :key="n.path">
          <td>
            <v-chip size="x-small" label :color="n.level === 0 ? 'primary' : undefined"
                    :variant="n.level === 0 ? 'flat' : 'tonal'">
              {{ n.level === 0 ? 'Assembly' : `L${n.level}` }}
            </v-chip>
          </td>
          <td>
            <span class="tree-cell" :style="{ paddingLeft: `${n.level * 16}px` }">
              <span v-if="n.level > 0" class="tree-branch">└</span>
              <span :class="n.level === 0 ? 'font-weight-bold' : ''">{{ n.item || '—' }}</span>
            </span>
          </td>
          <td>{{ n.itemDesc || '—' }}</td>
          <td>{{ n.mrn || '—' }}</td>
          <td>{{ n.mrnDesc || '—' }}</td>
        </tr>
      </tbody>
    </v-table>
  </div>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useReportStore } from '@/store/reportStore'

  const props = defineProps({ workOrderNumber: { type: String, required: true } })
  const store = useReportStore()
  const error = ref('')

  const nodes = computed(() => store.treeByWorkOrder[props.workOrderNumber] ?? [])
  const loading = computed(() => !!store.loadingTrees[props.workOrderNumber] || (!error.value && !store.treeByWorkOrder[props.workOrderNumber]))

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

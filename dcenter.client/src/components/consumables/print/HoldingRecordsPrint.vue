<template>
  <PrintSheet title="Electrode Holding Record" :subtitle="subtitle" :loading="loading" :error="error"
              :signatures="['Prepared by', 'Checked by']">
    <table class="print-table">
      <thead>
        <tr>
          <th>Holding No</th>
          <th>Date</th>
          <th>Welder Name</th>
          <th>Baking No</th>
          <th>Classification</th>
          <th>Brand</th>
          <th>Lot / Heat No.</th>
          <th>Oven / Compartment</th>
          <th class="num">Qty (KG)</th>
          <th>Remarks</th>
          <th>Entered by</th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="!loading && !rows.length"><td colspan="11">No holding records for this selection.</td></tr>
        <tr v-for="h in rows" :key="h.id" :style="h.isVoided ? 'text-decoration: line-through; color: #777;' : ''">
          <td>{{ h.holdingNo }}</td>
          <td>{{ fmtDate(h.holdingDate) }}</td>
          <td>{{ h.welderName }}</td>
          <td>{{ h.bakingNo }}</td>
          <td>{{ h.diaSpec }}</td>
          <td>{{ h.brand }}</td>
          <td>{{ h.lotNumber }}</td>
          <td>{{ h.isFinishedAfterBaking ? 'Finished After Baking' : h.compartmentLabel }}</td>
          <td class="num">{{ kg(h.quantityKg) }}</td>
          <td>{{ h.isVoided ? 'VOIDED' : '' }} {{ h.remarks }}</td>
          <td>{{ h.createdBy }}</td>
        </tr>
      </tbody>
    </table>
  </PrintSheet>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { errorText, fmtDate, kg } from '@/utils/consumables'
  import { loadAllPages, rangeText } from '@/components/consumables/print/printData'
  import PrintSheet from '@/components/consumables/print/PrintSheet.vue'

  const props = defineProps({ query: { type: Object, default: () => ({}) } })

  const store = useConsumableStore()
  const rows = ref([])
  const loading = ref(true)
  const error = ref('')

  const subtitle = computed(() =>
    [rangeText(props.query.from, props.query.to), props.query.q ? `Search: ${props.query.q}` : ''].filter(Boolean).join(' · '))

  onMounted(async () => {
    try {
      const items = await loadAllPages((skip, take) => store.loadHoldingRecords({ ...props.query, skip, take }))
      rows.value = items.sort((a, b) => a.id - b.id)
    } catch (e) {
      error.value = errorText(e, 'Could not load holding records.')
    } finally {
      loading.value = false
    }
  })
</script>

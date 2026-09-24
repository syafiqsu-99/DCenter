<template>
  <PrintSheet :title="`Stock Count Sheet — ${scopeLabel}`" :subtitle="subtitle" :loading="loading" :error="error"
              :signatures="['Counted by', 'Checked by']">
    <table class="print-table">
      <thead>
        <tr>
          <th>Location</th>
          <th>Type</th>
          <th>Classification</th>
          <th>Brand</th>
          <th>Lot / Heat No.</th>
          <th class="num">System (KG)</th>
          <th class="blank">Counted (KG)</th>
          <th class="blank">Remarks</th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="!loading && !lines.length"><td colspan="8">Nothing in this storage.</td></tr>
        <tr v-for="l in lines" :key="l.key">
          <td>{{ l.location }}</td>
          <td>{{ l.category }}</td>
          <td>{{ l.diaSpec }}</td>
          <td>{{ l.brand }}</td>
          <td>{{ l.lotNumber }}</td>
          <td class="num">{{ kg(l.systemKg) }}</td>
          <td class="blank" />
          <td class="blank" />
        </tr>
      </tbody>
    </table>
    <p style="margin-top:8px;">Record any stock found that is not listed on a separate line at the end of this sheet.</p>
  </PrintSheet>
</template>

<script setup>
  import { computed, onMounted, ref } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { ALL, errorText, fmtDateTime, kg } from '@/utils/consumables'
  import PrintSheet from '@/components/consumables/print/PrintSheet.vue'

  const props = defineProps({ query: { type: Object, default: () => ({}) } })

  const store = useConsumableStore()
  const lines = ref([])
  const generatedAt = ref('')
  const loading = ref(true)
  const error = ref('')

  const scopeLabel = computed(() => (props.query.scope === 'Normal' ? 'Main store' : 'Racks & ovens'))
  const subtitle = computed(() =>
    `${props.query.category || 'All types'} · System quantities as of ${generatedAt.value}`)

  onMounted(async () => {
    try {
      const sheet = await store.loadCountSheet(props.query.scope || 'Activated', props.query.category || ALL)
      lines.value = sheet.lines
      generatedAt.value = fmtDateTime(sheet.generatedAt)
    } catch (e) {
      error.value = errorText(e, 'Could not load the count sheet.')
    } finally {
      loading.value = false
    }
  })
</script>

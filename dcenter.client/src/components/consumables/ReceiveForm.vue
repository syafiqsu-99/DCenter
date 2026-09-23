<template>
  <v-card ref="card" border flat class="d-flex flex-column" :style="{ height }">
    <v-card-item>
      <template #prepend><v-avatar color="success" variant="tonal" icon="mdi-tray-arrow-down" /></template>
      <v-card-title>Stock In</v-card-title>
      <v-card-subtitle>Record consumables received into Weldshop or Tool Crib</v-card-subtitle>
    </v-card-item>
    <v-divider />

    <v-card-text class="flex-grow-1 overflow-y-auto">
      <div class="text-overline text-medium-emphasis">Receipt details</div>
      <v-row dense align="start">
        <v-col cols="12" sm="6" md="3">
          <v-text-field v-model="form.txnDate" type="date" :label="COLUMN.date" :max="maxDate" v-bind="field" />
        </v-col>
        <v-col cols="12" sm="6" md="4">
          <LocationToggle v-model="form.location" :label="COLUMN.source" block density="default" />
        </v-col>
        <v-col cols="12" md="5">
          <RequestorField v-model="form.requestor" :label="COLUMN.requestor" />
        </v-col>
      </v-row>

      <div class="text-overline text-medium-emphasis mt-4">Consumable</div>
      <template v-if="!newMode">
        <ConsumablePicker ref="picker" v-model="consumable" @new="startNew" />
        <DetailGrid v-if="consumable" :items="consumableDetails" md="2" class="mt-3" />
      </template>
      <v-sheet v-else border rounded class="pa-3">
        <div class="d-flex align-center mb-3">
          <span class="text-subtitle-2">New consumable</span>
          <v-spacer />
          <v-btn size="small" variant="text" prepend-icon="mdi-magnify" @click="cancelNew">Search existing instead</v-btn>
        </div>
        <ConsumableFields v-model="newItem" />
      </v-sheet>

      <div class="text-overline text-medium-emphasis mt-4">Quantity</div>
      <v-row dense align="start">
        <v-col cols="12" md="5">
          <v-combobox ref="lotField" v-model="form.lotNumber" :items="lots" :label="COLUMN.lot" v-bind="field"
                      :disabled="!hasItem" :hint="isNewLot ? 'New lot — it will be created on save' : ''"
                      persistent-hint />
        </v-col>
        <v-col cols="12" sm="6" md="3">
          <v-text-field v-model.number="form.quantityKg" type="number" min="0.01" step="0.01"
                        :label="COLUMN.receiveQty" suffix="kg" v-bind="field" :disabled="!hasItem"
                        @keydown.enter.prevent="save(false)" />
          <div v-if="quickQty.length" class="d-flex flex-wrap ga-1 mt-1">
            <v-chip v-for="q in quickQty" :key="q" size="small" variant="tonal" @click="form.quantityKg = q">
              {{ kg(q) }} kg
            </v-chip>
          </div>
        </v-col>
        <v-col cols="12" sm="6" md="4">
          <v-text-field v-model="form.remarks" label="Remarks" v-bind="field" />
        </v-col>
      </v-row>

      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-4">{{ error }}</v-alert>
    </v-card-text>

    <v-divider />
    <v-card-actions class="px-4 py-3">
      <span class="text-body-2 text-medium-emphasis">{{ summary }}</span>
      <v-spacer />
      <v-btn variant="text" :disabled="!canSave" :loading="saving" @click="save(true)">Save &amp; close</v-btn>
      <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :disabled="!canSave" :loading="saving"
             @click="save(false)">
        Save &amp; next
      </v-btn>
    </v-card-actions>
  </v-card>

  <v-snackbar v-model="snackbar" color="success" timeout="4000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import { computed, nextTick, reactive, ref, watch } from 'vue'
  import { useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useFillHeight } from '@/composables/useFillHeight'
  import { COLUMN, errorText, kg, todayIso } from '@/utils/consumables'
  import LocationToggle from '@/components/consumables/LocationToggle.vue'
  import RequestorField from '@/components/consumables/RequestorField.vue'
  import ConsumablePicker from '@/components/consumables/ConsumablePicker.vue'
  import ConsumableFields from '@/components/consumables/ConsumableFields.vue'
  import DetailGrid from '@/components/consumables/DetailGrid.vue'

  const DEFAULT_MIN_STOCK_KG = 10

  const store = useConsumableStore()
  const router = useRouter()
  const card = ref(null)
  const { height } = useFillHeight(card)
  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const maxDate = todayIso()

  const blankItem = () => ({ consumableType: null, manufacturer: '', specification: '', diameter: '', minStockKg: DEFAULT_MIN_STOCK_KG })

  const form = reactive({
    txnDate: todayIso(),
    location: store.header.receiveLocation,
    requestor: store.header.receivedBy,
    lotNumber: '',
    quantityKg: null,
    remarks: '',
  })

  const picker = ref(null)
  const lotField = ref(null)
  const consumable = ref(null)
  const newMode = ref(false)
  const newItem = ref(blankItem())
  const lots = ref([])
  const quickQty = ref([])
  const saving = ref(false)
  const error = ref('')
  const snackbar = ref(false)
  const snackbarText = ref('')

  const text = (v) => (v ?? '').toString().trim()
  const newItemValid = computed(() =>
    !!newItem.value.consumableType && !!text(newItem.value.manufacturer)
    && !!text(newItem.value.specification) && !!text(newItem.value.diameter)
    && Number(newItem.value.minStockKg || 0) >= 0)
  const hasItem = computed(() => (newMode.value ? newItemValid.value : !!consumable.value))
  const lotText = computed(() => text(form.lotNumber))
  const isNewLot = computed(() => !!lotText.value && !lots.value.some((l) => l.toLowerCase() === lotText.value.toLowerCase()))
  const lowStock = computed(() =>
    consumable.value && consumable.value.minStockKg > 0 && consumable.value.balanceKg <= consumable.value.minStockKg)
  const canSave = computed(() =>
    hasItem.value && !!form.location && !!lotText.value && Number(form.quantityKg) > 0
    && !!form.txnDate && form.txnDate <= maxDate && !saving.value)

  const consumableDetails = computed(() => {
    const c = consumable.value
    return [
      { label: COLUMN.type, value: c.consumableType },
      { label: COLUMN.brand, value: c.manufacturer },
      { label: COLUMN.diameter, value: c.diameter },
      { label: COLUMN.spec, value: c.specification },
      { label: `${COLUMN.balance} (all sources)`, value: `${kg(c.balanceKg)} kg`, class: lowStock.value ? 'text-warning' : '' },
      { label: COLUMN.minStock, value: c.minStockKg > 0 ? `${kg(c.minStockKg)} kg` : '' },
    ]
  })

  const summary = computed(() => {
    const qty = Number(form.quantityKg)
    if (!hasItem.value || !lotText.value || !(qty > 0)) return 'Fill in the consumable, lot and quantity.'
    return `Adds ${kg(qty)} kg to lot ${lotText.value} at ${form.location}.`
  })

  watch(consumable, async (c) => {
    form.lotNumber = ''
    lots.value = []
    quickQty.value = []
    if (!c) return
    try {
      const [lotList, quantities] = await Promise.all([store.lotsFor(c.id), store.recentQuantities(c.id)])
      if (consumable.value?.id !== c.id) return
      lots.value = lotList.map((l) => l.lotNumber)
      quickQty.value = quantities
      nextTick(() => lotField.value?.focus())
    } catch {
      lots.value = []
    }
  })

  function startNew() {
    consumable.value = null
    newItem.value = blankItem()
    newMode.value = true
  }

  function cancelNew() {
    newMode.value = false
    nextTick(() => picker.value?.focus())
  }

  function resetItem() {
    consumable.value = null
    newMode.value = false
    newItem.value = blankItem()
    form.lotNumber = ''
    form.quantityKg = null
    form.remarks = ''
    nextTick(() => picker.value?.focus())
  }

  async function save(close) {
    if (!canSave.value) return
    saving.value = true
    error.value = ''
    try {
      const result = await store.receive({
        txnDate: form.txnDate,
        location: form.location,
        requestor: text(form.requestor) || null,
        consumableId: newMode.value ? null : consumable.value.id,
        newConsumable: newMode.value
          ? {
              consumableType: newItem.value.consumableType,
              manufacturer: text(newItem.value.manufacturer),
              specification: text(newItem.value.specification),
              diameter: text(newItem.value.diameter),
              minStockKg: Number(newItem.value.minStockKg ?? DEFAULT_MIN_STOCK_KG) || 0,
              isActive: true,
            }
          : null,
        lotNumber: lotText.value,
        quantityKg: Number(form.quantityKg),
        remarks: text(form.remarks) || null,
      })
      store.rememberHeader({ receiveLocation: form.location, receivedBy: text(form.requestor) })
      const t = result.transaction
      snackbarText.value = `Received ${kg(t.quantityKg)} kg — ${t.diaSpec}, lot ${t.lotNumber} at ${t.location}. Lot balance ${kg(result.balanceKg)} kg.`
      snackbar.value = true
      if (close) {
        router.push({ name: 'consumable-inventory' })
        return
      }
      resetItem()
    } catch (e) {
      error.value = errorText(e, 'Could not save this stock in.')
    } finally {
      saving.value = false
    }
  }
</script>

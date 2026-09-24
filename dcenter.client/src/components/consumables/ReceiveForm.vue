<template>
  <v-card border flat class="fill-card">
    <v-card-item>
      <template #prepend><v-avatar color="success" variant="tonal" icon="mdi-tray-arrow-down" /></template>
      <v-card-title>Receive stock</v-card-title>
      <v-card-subtitle>New stock goes into Normal storage</v-card-subtitle>
    </v-card-item>
    <v-divider />

    <v-card-text class="fill overflow-y-auto">
      <div class="text-overline text-medium-emphasis">Receipt</div>
      <v-row dense align="start">
        <v-col cols="12" sm="6" md="3">
          <v-text-field v-model="form.txnDate" type="date" :label="COLUMN.date" :max="maxDate" v-bind="field" />
        </v-col>
        <v-col cols="12" sm="6" md="5">
          <v-btn-toggle v-model="form.source" mandatory divided variant="outlined" color="primary" class="w-100"
                        density="comfortable">
            <v-btn v-for="s in store.catalog.sources" :key="s" :value="s" class="flex-grow-1">{{ s }}</v-btn>
          </v-btn-toggle>
          <div class="text-caption text-medium-emphasis mt-1">{{ COLUMN.source }} (origin)</div>
        </v-col>
        <v-col cols="12" md="4" class="d-flex align-center text-body-2 text-medium-emphasis">
          <v-icon class="me-2">mdi-account-check-outline</v-icon>Received by {{ store.enteredBy }}
        </v-col>
      </v-row>

      <div class="d-flex align-center text-overline text-medium-emphasis mt-4">
        Consumable
        <v-spacer />
        <v-switch v-model="isNew" label="New consumable" color="primary" density="compact" hide-details inset />
      </div>
      <ItemPicker v-if="!isNew" ref="picker" v-model="item" />
      <ItemFields v-else v-model="newItem" />

      <div class="text-overline text-medium-emphasis mt-4">Lot</div>
      <v-row dense>
        <v-col cols="12" md="6">
          <v-combobox v-model="form.brand" :items="brands" :label="COLUMN.brand" v-bind="field" />
        </v-col>
        <v-col cols="12" md="6">
          <v-combobox v-model="form.lotNumber" :items="lotNumbers" :label="COLUMN.lot" v-bind="field"
                      :hint="lotHint" persistent-hint />
        </v-col>
      </v-row>

      <div class="text-overline text-medium-emphasis mt-4">Quantity</div>
      <v-row dense align="start">
        <v-col cols="12" sm="6" md="4">
          <v-text-field ref="qtyField" v-model.number="form.quantityKg" type="number" min="0.01" step="0.01"
                        :label="COLUMN.receiveQty" suffix="kg" v-bind="field" @keydown.enter.prevent="save" />
          <div class="d-flex flex-wrap ga-1 mt-1">
            <v-chip v-for="q in quickQuantities" :key="q" size="small" variant="tonal" @click="form.quantityKg = q">
              {{ kg(q) }}
            </v-chip>
          </div>
        </v-col>
        <v-col cols="12" sm="6" md="8">
          <v-text-field v-model="form.remarks" label="Remarks" v-bind="field" />
        </v-col>
      </v-row>

      <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-4">{{ error }}</v-alert>
    </v-card-text>

    <v-divider />
    <v-card-actions class="px-4 py-3">
      <span class="text-body-2 text-medium-emphasis">{{ summary }}</span>
      <v-spacer />
      <v-btn color="primary" variant="flat" prepend-icon="mdi-content-save" :disabled="!canSave" :loading="saving" @click="save">
        Save &amp; next
      </v-btn>
    </v-card-actions>
  </v-card>

  <v-snackbar v-model="snackbar" color="success" timeout="4000">{{ snackbarText }}</v-snackbar>
</template>

<script setup>
  import { computed, nextTick, reactive, ref, watch } from 'vue'
  import { storeToRefs } from 'pinia'
  import { useConsumableStore } from '@/store/consumableStore'
  import { useLookupStore } from '@/store/lookupStore'
  import { COLUMN, ELECTRODE, errorText, kg, todayIso } from '@/utils/consumables'
  import ItemPicker from '@/components/consumables/ItemPicker.vue'
  import ItemFields from '@/components/consumables/ItemFields.vue'

  const store = useConsumableStore()
  const lookupStore = useLookupStore()
  const { options } = storeToRefs(lookupStore)
  lookupStore.load()

  const field = { variant: 'outlined', density: 'comfortable', hideDetails: 'auto' }
  const maxDate = todayIso()

  const form = reactive({
    txnDate: todayIso(),
    source: store.receiveHeader.source || store.catalog.sources[0],
    brand: '',
    lotNumber: '',
    quantityKg: null,
    remarks: '',
  })
  const isNew = ref(false)
  const item = ref(null)
  const newItem = ref(blankItem())
  const lots = ref([])
  const recent = ref([])
  const picker = ref(null)
  const qtyField = ref(null)
  const saving = ref(false)
  const error = ref('')
  const snackbar = ref(false)
  const snackbarText = ref('')

  function blankItem() {
    return {
      category: null, specification: '', diameter: '', minStockKg: 0, activatedMinKg: 0, finishThresholdKg: null, holdingOvenType: null,
    }
  }

  const text = (v) => (v ?? '').toString().trim()

  const brands = computed(() => {
    const fromLots = lots.value.map((l) => l.brand)
    return [...new Set([...fromLots, ...(options.value?.Manuf ?? [])])]
  })

  const lotNumbers = computed(() =>
    lots.value.filter((l) => !text(form.brand) || l.brand.toLowerCase() === text(form.brand).toLowerCase()).map((l) => l.lotNumber))

  const lotHint = computed(() => {
    if (!text(form.lotNumber)) return ''
    return lotNumbers.value.some((n) => n.toLowerCase() === text(form.lotNumber).toLowerCase())
      ? 'Existing lot — this receipt tops it up'
      : 'New lot'
  })

  const quickQuantities = computed(() => [...new Set([...recent.value, 5, 10, 20])].slice(0, 6))

  const itemReady = computed(() =>
    isNew.value
      ? !!newItem.value.category && !!text(newItem.value.specification) && !!text(newItem.value.diameter)
      : !!item.value)

  const canSave = computed(() =>
    store.hasEnteredBy && itemReady.value && !!form.source && !!text(form.brand) && !!text(form.lotNumber)
    && Number(form.quantityKg) > 0)

  const summary = computed(() => {
    const name = isNew.value ? `${text(newItem.value.diameter)} ${text(newItem.value.specification)}`.trim() : item.value?.diaSpec
    if (!name || !(Number(form.quantityKg) > 0)) return ''
    return `${kg(form.quantityKg)} kg of ${name} → Normal · ${form.source}`
  })

  watch(item, async (value) => {
    lots.value = []
    recent.value = []
    if (!value) return
    try {
      const [lotList, quantities] = await Promise.all([store.lotsFor(value.id), store.recentQuantities(value.id, 'Receive')])
      lots.value = lotList
      recent.value = quantities
      if (!text(form.brand) && lotList.length) form.brand = lotList[0].brand
    } catch {
      lots.value = []
    }
  })

  watch(isNew, () => {
    item.value = null
    newItem.value = blankItem()
  })

  async function save() {
    if (!canSave.value || saving.value) return
    saving.value = true
    error.value = ''
    try {
      const payload = {
        txnDate: form.txnDate,
        source: form.source,
        receivedBy: null,
        itemId: isNew.value ? null : item.value.id,
        newItem: isNew.value
          ? {
              ...newItem.value,
              minStockKg: Number(newItem.value.minStockKg) || 0,
              activatedMinKg: Number(newItem.value.activatedMinKg) || 0,
              finishThresholdKg: newItem.value.finishThresholdKg === '' || newItem.value.finishThresholdKg === null
                ? null
                : Number(newItem.value.finishThresholdKg),
              holdingOvenType: newItem.value.category === ELECTRODE ? newItem.value.holdingOvenType || null : null,
              isActive: true,
            }
          : null,
        brand: text(form.brand),
        lotNumber: text(form.lotNumber),
        quantityKg: Number(form.quantityKg),
        remarks: text(form.remarks) || null,
      }
      const result = await store.receive(payload)
      store.rememberReceiveHeader({ source: form.source })
      snackbarText.value = `${result.txnNo} saved — Normal balance ${kg(result.balance.normalKg)} kg`
      snackbar.value = true
      form.lotNumber = ''
      form.quantityKg = null
      form.remarks = ''
      if (isNew.value) {
        const line = result.lines[0]
        isNew.value = false
        await nextTick()
        item.value = {
          id: line.itemId, diaSpec: line.diaSpec, category: line.category,
          normalKg: result.balance.normalKg, activatedKg: result.balance.activatedKg,
        }
      }
      await nextTick()
      qtyField.value?.focus()
    } catch (e) {
      error.value = errorText(e, 'Could not save the receipt.')
    } finally {
      saving.value = false
    }
  }
</script>

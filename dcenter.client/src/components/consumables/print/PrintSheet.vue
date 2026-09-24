<template>
  <div class="print-sheet">
    <div class="no-print d-flex align-center ga-2 mb-4">
      <v-btn color="primary" variant="flat" prepend-icon="mdi-printer" :disabled="loading || !!error" @click="print">Print</v-btn>
      <v-btn variant="text" prepend-icon="mdi-close" @click="close">Close</v-btn>
      <v-progress-circular v-if="loading" indeterminate size="20" class="ms-2" />
    </div>
    <v-alert v-if="error" type="error" variant="tonal" class="no-print mb-4">{{ error }}</v-alert>

    <header class="print-header">
      <div>
        <div class="print-title">{{ title }}</div>
        <div v-if="subtitle" class="print-subtitle">{{ subtitle }}</div>
      </div>
      <div class="print-meta">
        <div>DCenter · Welding Consumables</div>
        <div>Printed {{ printedAt }}<template v-if="store.enteredBy"> by {{ store.enteredBy }}</template></div>
      </div>
    </header>

    <slot />

    <footer v-if="signatures.length" class="print-signatures">
      <div v-for="s in signatures" :key="s" class="print-signature">
        <div class="print-signature-line" />
        <div>{{ s }} — name, signature &amp; date</div>
      </div>
    </footer>
  </div>
</template>

<script setup>
  import { nextTick, watch } from 'vue'
  import { useConsumableStore } from '@/store/consumableStore'
  import { fmtDateTime } from '@/utils/consumables'

  const props = defineProps({
    title: { type: String, required: true },
    subtitle: { type: String, default: '' },
    loading: { type: Boolean, default: false },
    error: { type: String, default: '' },
    signatures: { type: Array, default: () => [] },
  })

  const store = useConsumableStore()
  const printedAt = fmtDateTime(new Date().toISOString())
  let printed = false

  function print() {
    window.print()
  }

  function close() {
    window.close()
  }

  watch(() => props.loading, async (loading) => {
    if (loading || props.error || printed) return
    printed = true
    await nextTick()
    setTimeout(print, 300)
  })
</script>

<style>
  .print-sheet {
    padding: 16px;
    color: #000;
    background: #fff;
    font-size: 12px;
  }
  .print-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-end;
    border-bottom: 2px solid #000;
    padding-bottom: 6px;
    margin-bottom: 10px;
  }
  .print-title {
    font-size: 18px;
    font-weight: 700;
  }
  .print-subtitle,
  .print-meta {
    font-size: 11px;
  }
  .print-meta {
    text-align: right;
  }
  .print-table {
    width: 100%;
    border-collapse: collapse;
  }
  .print-table th,
  .print-table td {
    border: 1px solid #555;
    padding: 3px 5px;
    vertical-align: top;
  }
  .print-table th {
    background: #eee;
    text-align: left;
  }
  .print-table .num {
    text-align: right;
    white-space: nowrap;
  }
  .print-table .blank {
    min-width: 80px;
  }
  .print-table tr {
    page-break-inside: avoid;
  }
  .print-signatures {
    display: flex;
    gap: 32px;
    margin-top: 32px;
  }
  .print-signature {
    flex: 1;
    font-size: 11px;
  }
  .print-signature-line {
    border-bottom: 1px solid #000;
    height: 36px;
  }
  @media print {
    @page {
      size: A4 landscape;
      margin: 10mm;
    }
    .no-print,
    .v-app-bar,
    .v-navigation-drawer,
    .v-footer {
      display: none !important;
    }
    .v-main {
      padding: 0 !important;
    }
    .print-sheet {
      padding: 0;
    }
    .print-table thead {
      display: table-header-group;
    }
  }
</style>

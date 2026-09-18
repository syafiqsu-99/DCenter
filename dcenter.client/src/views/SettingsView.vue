<template>
  <v-card flat border class="page-card">
    <div class="d-flex flex-grow-1 min-h-0">
      <v-list nav density="comfortable" width="230"
              class="d-none d-sm-block flex-shrink-0 py-4 overflow-y-auto border-e">
        <v-list-subheader>SETTINGS</v-list-subheader>
        <v-list-item v-for="s in sections" :key="s.value" :active="sub === s.value"
                     :prepend-icon="s.icon" :title="s.label" rounded="lg" class="mx-2"
                     @click="sub = s.value" />
      </v-list>

      <div class="flex-grow-1 pa-4 d-flex flex-column min-w-0 min-h-0">
        <v-select v-model="sub" :items="sections" item-title="label" item-value="value"
                  variant="outlined" density="comfortable" hide-details
                  class="d-sm-none mb-4 flex-shrink-0" prepend-inner-icon="mdi-cog-outline" />

        <div class="flex-grow-1 min-h-0">
          <component :is="active.component" v-bind="active.props" :key="active.value" />
        </div>
      </div>
    </div>
  </v-card>
</template>

<script setup>
  import { computed, ref } from 'vue';
  import WelderTable from '@/components/WelderTable.vue';
  import LookupTable from '@/components/LookupTable.vue';
  import ReferenceTable from '@/components/ReferenceTable.vue';
  import ProcessTypeLinkTable from '@/components/ProcessTypeLinkTable.vue';

  const wpsFields = [
    { key: 'wpsNo',     label: 'WPS No.',     width: '30%' },
    { key: 'baseMetal', label: 'Base Metal',  width: '30%' },
    { key: 'process',   label: 'Process',     width: '20%' },
    { key: 'pNo',       label: 'P-No.',       width: '10%' },
  ];

  const mrnFields = [
    { key: 'mrn', label: 'MRN', width: '18%' },
    { key: 'form', label: 'Form', width: '17%' },
    { key: 'fullSpecification', label: 'Full Specification', width: '35%' },
    { key: 'specNo', label: 'Spec No.', width: '15%' },
    { key: 'specNoRaw', label: 'Spec No. (Raw)', width: '15%' },
  ]

  const bpvcFields = [
    { key: 'specNo', label: 'Spec No.', width: '7%' },
    { key: 'specNoRaw', label: 'Spec No. (Raw)', width: '7%' },
    { key: 'designation', label: 'Designation / Alloy / Grade', width: '7%' },
    { key: 'unsNo', label: 'UNS No.', width: '7%' },
    { key: 'minTensile', label: 'Min. Tensile', width: '7%' },
    { key: 'pNo', label: 'P-No.', width: '7%' },
    { key: 'groupNo', label: 'Group No.', width: '7%' },
    { key: 'isoGroup', label: 'ISO 15608 Group', width: '7%' },
    { key: 'brazingPNo', label: 'Brazing P-No.', width: '7%' },
    { key: 'nominalComposition', label: 'Nominal Composition', width: '15%' },
    { key: 'typicalProductForm', label: 'Typical Product Form', width: '15%' },
    { key: 'nominalThicknessLimits', label: 'Nominal Thickness Limits', width: '5%' },
  ];

  const sections = [
    { value: 'welders', label: 'Welders', icon: 'mdi-account-hard-hat', component: WelderTable },
    { value: 'dropdowns', label: 'Dropdown Lists', icon: 'mdi-format-list-bulleted', component: LookupTable },
    { value: 'processType', label: 'Process → Type', icon: 'mdi-link-variant', component: ProcessTypeLinkTable },
    {
      value: 'wps', label: 'WPS No.', icon: 'mdi-clipboard-text-outline', component: ReferenceTable,
      props: {
        apiBase: '/wps', title: 'WPS Numbers', fields: wpsFields,
      },
    },
    {
      value: 'mrn', label: 'MRN', icon: 'mdi-table-key', component: ReferenceTable,
      props: {
        apiBase: '/mrn', title: 'MRN Numbers', fields: mrnFields,
      },
    },
    {
      value: 'bpvc', label: 'BPVC IX', icon: 'mdi-book-open-variant', component: ReferenceTable,
      props: {
        apiBase: '/bpvc', title: 'BPVC IX', fields: bpvcFields,
      },
    },
  ];

  const sub = ref('welders');
  const active = computed(() => sections.find((s) => s.value === sub.value));
</script>

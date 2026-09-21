<template>
  <div :style="wrap">
    <div :style="titleStyle">Joint {{ joint.jointNumber }}</div>

    <table :style="tbl">
      <colgroup>
        <col style="width:12%;" />
        <col style="width:19%;" />
        <col style="width:12%;" />
        <col style="width:19%;" />
        <col style="width:12%;" />
        <col style="width:8.66%;" />
        <col style="width:8.66%;" />
        <col style="width:8.66%;" />
      </colgroup>
      <tbody>
        <!-- Section header row -->
        <tr>
          <td :style="hdr" colspan="2">Joining of</td>
          <td :style="hdr" colspan="2">with</td>
          <td :style="hdr">Electrode Data:</td>
          <td :style="hdrC"></td>
          <td :style="hdrC"></td>
          <td :style="hdrC"></td>
        </tr>

        <!-- Part Desc | Part Desc | Process -->
        <tr>
          <td :style="lbl">Part Desc. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partDescLeft" :item-title="partDescTitle"
                        @update:model-value="(v) => setPart('Left', 'desc', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Part Desc. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partDescRight" :item-title="partDescTitle"
                        @update:model-value="(v) => setPart('Right', 'desc', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Process</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].process" :items="opt('Process', joint.materials[0].process)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].process" :items="opt('Process', joint.materials[1].process)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].process" :items="opt('Process', joint.materials[2].process)" v-bind="f" clearable /></td>
        </tr>

        <!-- Part No | Part No | Size -->
        <tr>
          <td :style="lbl">Part No. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partNoLeft" :item-title="partNoTitle"
                        @update:model-value="(v) => setPart('Left', 'no', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Part No. :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :items="rightParts"
                        :model-value="joint.partNoRight" :item-title="partNoTitle"
                        @update:model-value="(v) => setPart('Right', 'no', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.no ?? item.raw" :subtitle="item.raw?.desc" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Size</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].size" :items="opt('Size', joint.materials[0].size)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].size" :items="opt('Size', joint.materials[1].size)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].size" :items="opt('Size', joint.materials[2].size)" v-bind="f" clearable /></td>
        </tr>

        <!-- Heat Number | Heat Number | Type -->
        <tr>
          <td :style="lbl">Heat Number :</td>
          <td :style="cell"><v-text-field v-model="joint.heatNumberLeft" v-bind="f" /></td>
          <td :style="lbl">Heat Number :</td>
          <td :style="cell"><v-text-field v-model="joint.heatNumberRight" v-bind="f" /></td>
          <td :style="lbl">Type</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].type" :items="typeOpt(0)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].type" :items="typeOpt(1)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].type" :items="typeOpt(2)" v-bind="f" clearable /></td>
        </tr>

        <!-- WPS No | Rev | Manuf -->
        <tr>
          <td :style="lbl">WPS No.:</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :custom-filter="allowAll" v-model="joint.wpsNo" :items="wpsNos"
                        :loading="loadingWpsFilter" :messages="wpsHint" @update:search="searchWps" />
          </td>
          <td :style="lbl">Rev:</td>
          <td :style="cell"><v-text-field v-model="joint.rev" v-bind="f" /></td>
          <td :style="lbl">Manuf</td>
          <td :style="cell"><v-combobox v-model="joint.materials[0].manuf" :items="opt('Manuf', joint.materials[0].manuf)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[1].manuf" :items="opt('Manuf', joint.materials[1].manuf)" v-bind="f" clearable /></td>
          <td :style="cell"><v-combobox v-model="joint.materials[2].manuf" :items="opt('Manuf', joint.materials[2].manuf)" v-bind="f" clearable /></td>
        </tr>

        <!-- Welder Name | Welder No | Heat/Lot -->
        <tr>
          <td :style="lbl">Welder Name :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :custom-filter="allowAll" :items="welderItems"
                        :model-value="joint.welderName" :item-title="welderNameTitle"
                        @update:search="searchWelders" @update:model-value="(v) => setWelder('name', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.welderName ?? item.raw" :subtitle="item.raw?.welderNo" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Welder No :</td>
          <td :style="cell">
            <v-combobox v-bind="f" clearable :custom-filter="allowAll" :items="welderItems"
                        :model-value="joint.welderNo" :item-title="welderNoTitle"
                        @update:search="searchWelders" @update:model-value="(v) => setWelder('no', v)">
              <template #item="{ props: p, item }">
                <v-list-item v-bind="p" :title="item.raw?.welderName ?? item.raw" :subtitle="item.raw?.welderNo" />
              </template>
            </v-combobox>
          </td>
          <td :style="lbl">Heat/Lot</td>
          <td :style="cell"><v-text-field v-model="joint.materials[0].heatLot" v-bind="f" /></td>
          <td :style="cell"><v-text-field v-model="joint.materials[1].heatLot" v-bind="f" /></td>
          <td :style="cell"><v-text-field v-model="joint.materials[2].heatLot" v-bind="f" /></td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup>
  import { computed, ref, watch } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useReportStore } from '@/store/reportStore';
  import { useLookupStore } from '@/store/lookupStore';
  import { useProcessTypeStore } from '@/store/processTypeStore';
  import { useWpsStore } from '@/store/wpsStore';
  import api from '@/utils/api';

  const wpsStore = useWpsStore()
  wpsStore.load();

  const props = defineProps({
    joint: { type: Object, required: true },
  });

  const processTypeStore = useProcessTypeStore()
  processTypeStore.load()

  function typeOpt(col) {
    const linked = processTypeStore.typesForProcess(props.joint.materials[col].process)
    const base = linked ?? (options.value['Type'] ?? [])
    const current = props.joint.materials[col].type
    return current && !base.includes(current) ? [current, ...base] : base
  }

  const f = { density: 'compact', variant: 'plain', hideDetails: true };
  const allowAll = () => true;

  const reportStore = useReportStore();
  const { rightParts } = storeToRefs(reportStore);

  const lookupStore = useLookupStore();
  const { options } = storeToRefs(lookupStore);

  function opt(category, current) {
    const list = options.value[category] ?? [];
    return current && !list.includes(current) ? [current, ...list] : list;
  }

  function isEmpty(v) {
    return v === null || v === undefined || v === '';
  }

  function partNoTitle(p) {
    return typeof p === 'string' ? p : (p?.no ?? '')
  }
  function partDescTitle(p) {
    return typeof p === 'string' ? p : (p?.desc ?? '')
  }
  function setPart(side, field, v) {
    const j = props.joint
    const noKey = `partNo${side}`
    const descKey = `partDesc${side}`
    if (v && typeof v === 'object') {
      j[noKey] = v.no ?? ''
      j[descKey] = v.desc ?? ''
      return
    }
    const val = v ?? ''
    const hit = rightParts.value.find((p) => (field === 'no' ? p.no : p.desc) === val)
    if (hit) {
      j[noKey] = hit.no
      j[descKey] = hit.desc
    } else {
      j[field === 'no' ? noKey : descKey] = val
    }
  }

  const welderItems = ref([]);
  async function searchWelders(q) {
    const { data } = await api.get('/welders/search', { params: { q: q || '' } });
    welderItems.value = data;
  }
  searchWelders('');

  function welderNameTitle(w) {
    return typeof w === 'string' ? w : (w?.welderName ?? '')
  }
  function welderNoTitle(w) {
    return typeof w === 'string' ? w : (w?.welderNo ?? '')
  }
  function setWelder(field, v) {
    const j = props.joint
    if (v && typeof v === 'object') {
      j.welderName = v.welderName ?? ''
      j.welderNo = v.welderNo ?? ''
      return
    }
    const val = v ?? ''
    const hit = welderItems.value.find((w) => (field === 'name' ? w.welderName : w.welderNo) === val)
    if (hit) {
      j.welderName = hit.welderName
      j.welderNo = hit.welderNo
    } else {
      j[field === 'name' ? 'welderName' : 'welderNo'] = val
    }
  }

  const { allowedWps, wpsFilterNote, loadingWpsFilter, filterPNos } = storeToRefs(reportStore)
  const wpsQuery = ref('')

  const hasFilter = computed(() => filterPNos.value.length > 0)

  const wpsPool = computed(() =>
    hasFilter.value
      ? allowedWps.value
      : wpsStore.items.map((w) => ({ wpsNo: w.wpsNo, process: w.process, baseMetal: w.baseMetal, pNo: w.pNo })))

  const wpsItems = computed(() => {
    const q = wpsQuery.value.trim().toLowerCase()
    if (!q) return wpsPool.value
    return wpsPool.value.filter(
      (w) => w.wpsNo.toLowerCase().includes(q) || (w.process ?? '').toLowerCase().includes(q))
  })

  const wpsNos = computed(() => [...new Set(wpsItems.value.map((x) => x.wpsNo))])

  const wpsHint = computed(() =>
    hasFilter.value
      ? (wpsFilterNote.value ?? `Filtered to P-No ${filterPNos.value.join(', ')}`)
      : 'Showing all WPS — fill a P# above to narrow.')

  function searchWps(q) {
    wpsQuery.value = q || ''
  }

  const wrap = 'background:#fff;padding:12px;border:1px solid #000;margin-bottom:16px;overflow-x:auto;';
  const titleStyle = 'font-weight:bold;font-size:14px;margin-bottom:6px;';
  const tbl = 'border-collapse:collapse;width:100%;table-layout:fixed;';
  const cell = 'border:1px solid #000;padding:0 4px;font-size:12px;vertical-align:middle;height:26px;';
  const lbl = 'border:1px solid #000;padding:0 4px;font-size:12px;font-weight:bold;white-space:nowrap;vertical-align:middle;';
  const hdr = 'border:1px solid #000;padding:2px 4px;font-size:12px;font-weight:bold;text-align:center;';
  const hdrC = 'border:1px solid #000;';
</script>

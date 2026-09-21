<template>
  <div :style="wrap">
    <div :style="titleStyle">WELD SHOP JOB REPORT</div>

    <table :style="tbl">
      <tbody>
        <tr>
          <!-- Left: report meta -->
          <td style="width:50%;vertical-align:top;padding-right:12px;">
            <table :style="innerTbl">
              <tbody>
                <tr>
                  <td :style="lbl">Report Required?</td>
                  <td :style="cell">
                    <v-select v-model="report.reportRequired" :items="yesNo" item-title="t"
                              item-value="v" v-bind="f" />
                  </td>
                </tr>
                <tr>
                  <td :style="lbl">Date Welded <span style="color:#c62828;">*</span> :</td>
                  <td :style="dateCell">
                    <v-text-field v-model="report.dateWelded" type="date" v-bind="f"
                                  :color="dateWeldedMissing ? 'error' : undefined" />
                  </td>
                </tr>
                <tr>
                  <td :style="lbl">Work Order:</td>
                  <td :style="cell">
                    <v-combobox v-if="isDuplicate" v-model="report.workOrderNumber"
                                :items="allWorkOrderNumbers" :loading="loadingWorkOrderNumbers"
                                v-bind="f" clearable
                                @update:model-value="onWorkOrderPick" />
                    <v-text-field v-else v-model="report.workOrderNumber" v-bind="f" readonly />
                  </td>
                </tr>
                <tr>
                  <td :style="lbl">Part No. :</td>
                  <td :style="cell">
                    <v-text-field v-model="report.partNo" v-bind="f" :readonly="lockedFromSearch" />
                  </td>
                </tr>
                <tr>
                  <td :style="lbl">Description:</td>
                  <td :style="cell">
                    <v-text-field v-model="report.description" v-bind="f" :readonly="lockedFromSearch" />
                  </td>
                </tr>
              </tbody>
            </table>
          </td>

          <!-- Right: material spec / grade / P# across columns 1-3 -->
          <td style="width:50%;vertical-align:top;">
            <table :style="innerTbl">
              <tbody>
                <tr>
                  <td :style="lbl"></td>
                  <td :style="hdrC">1</td>
                  <td :style="hdrC">2</td>
                  <td :style="hdrC">3</td>
                </tr>
                <tr>
                  <td :style="lblR">Material:</td>
                  <td :style="cell"><v-combobox v-model="report.materialSpec1" :items="materialOptions" v-bind="f" clearable @update:model-value="onMaterial(1, $event)" /></td>
                  <td :style="cell"><v-combobox v-model="report.materialSpec2" :items="materialOptions" v-bind="f" clearable @update:model-value="onMaterial(2, $event)" /></td>
                  <td :style="cell"><v-combobox v-model="report.materialSpec3" :items="materialOptions" v-bind="f" clearable @update:model-value="onMaterial(3, $event)" /></td>
                </tr>
                <tr>
                  <td :style="lblR">Grade:</td>
                  <td :style="cell"><v-combobox v-model="report.grade1" :items="gradeOptions" v-bind="f" clearable @update:model-value="onGrade(1, $event)" /></td>
                  <td :style="cell"><v-combobox v-model="report.grade2" :items="gradeOptions" v-bind="f" clearable @update:model-value="onGrade(2, $event)" /></td>
                  <td :style="cell"><v-combobox v-model="report.grade3" :items="gradeOptions" v-bind="f" clearable @update:model-value="onGrade(3, $event)" /></td>
                </tr>
                <tr>
                  <td :style="lblR">P#:</td>
                  <td :style="cell"><v-text-field v-model="report.pNumber1" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.pNumber2" v-bind="f" /></td>
                  <td :style="cell"><v-text-field v-model="report.pNumber3" v-bind="f" /></td>
                </tr>
              </tbody>
            </table>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <div class="d-flex align-center ga-3 mb-3">
    <v-text-field :model-value="jointCount" type="number" min="1" max="50" density="compact"
                  variant="outlined" hide-details style="max-width:160px;"
                  label="Joints to insert" @update:model-value="store.setJointCount($event)" />
    <span class="text-caption text-medium-emphasis">Defaults to the number of parts found for this work order, max 50.</span>
  </div>

  <JointForm v-for="joint in report.joints" :key="joint.jointNumber" :joint="joint" />
</template>

<script setup>
  import { computed, onMounted } from 'vue';
  import { storeToRefs } from 'pinia';
  import { useReportStore } from '@/store/reportStore';
  import { useLookupStore } from '@/store/lookupStore';
  import JointForm from '@/components/weld/JointForm.vue';
  import { useBpvcStore } from '@/store/bpvcStore';

  const store = useReportStore();
  const { report, jointCount, mode, allWorkOrderNumbers, loadingWorkOrderNumbers } = storeToRefs(store);
  const isDuplicate = computed(() => mode.value === 'duplicate');
  const lockedFromSearch = computed(() => mode.value === 'new');

  useLookupStore().load(true);

  function onWorkOrderPick(v) {
    store.autofillFromWorkOrder(v);
  }

  const bpvc = useBpvcStore();
  bpvc.load();
  const { materialOptions, gradeOptions } = storeToRefs(bpvc);

  function fillPNo(col, spec, grade) {
    const pno = bpvc.resolvePNo(spec, grade);
    if (pno) report.value[`pNumber${col}`] = pno;
  }
  function onMaterial(col, spec) {
    fillPNo(col, spec, report.value[`grade${col}`]);
  }
  function onGrade(col, grade) {
    fillPNo(col, report.value[`materialSpec${col}`], grade);
  }

  const yesNo = [{ t: 'YES', v: true }, { t: 'NO', v: false }];

  const f = { density: 'compact', variant: 'plain', hideDetails: true };

  const wrap = 'background:#fff;padding:12px;border:1px solid #000;margin-bottom:16px;overflow-x:auto;';
  const titleStyle = 'text-align:center;font-weight:bold;font-size:15px;text-decoration:underline;margin-bottom:10px;';
  const tbl = 'border-collapse:collapse;width:100%;';
  const innerTbl = 'border-collapse:collapse;width:100%;table-layout:fixed;';
  const cell = 'border:1px solid #000;padding:0 4px;font-size:12px;vertical-align:middle;height:26px;';
  const lbl = 'border:1px solid #000;padding:0 4px;font-size:12px;font-weight:bold;white-space:nowrap;vertical-align:middle;width:35%;';
  const lblR = lbl + 'text-align:right;';
  const hdrC = 'border:1px solid #000;padding:0 4px;font-size:12px;font-weight:bold;text-align:center;';

  const dateWeldedMissing = computed(() => !report.value.dateWelded);
  const dateCell = computed(() =>
    dateWeldedMissing.value ? cell + 'background:#fdecea;' : cell);

  onMounted(() => {
    if (isDuplicate.value) store.loadAllWorkOrderNumbers();
  });
</script>

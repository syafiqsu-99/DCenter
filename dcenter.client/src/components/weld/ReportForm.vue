<template>
  <div style="background:#fff;padding:12px;border:1px solid #000;margin-bottom:16px;overflow-x:auto;">
    <div style="text-align:center;font-weight:bold;font-size:15px;text-decoration:underline;margin-bottom:10px;">
      WELD SHOP JOB REPORT
    </div>

    <!-- Header: left field block + right 1/2/3 material table -->
    <table style="border-collapse:collapse;width:100%;">
      <tbody>
        <tr>
          <!-- Left column: report meta -->
          <td style="width:50%;vertical-align:top;padding-right:12px;">
            <table style="border-collapse:collapse;width:100%;">
              <tbody>
                <tr>
                  <td :style="labelCell">Report Required?</td>
                  <td :style="cell">
                    <select v-model="report.reportRequired" :style="input">
                      <option :value="true">YES</option>
                      <option :value="false">NO</option>
                    </select>
                  </td>
                </tr>
                <tr>
                  <td :style="labelCell">Date Welded :</td>
                  <td :style="cell"><input v-model="report.dateWelded" type="date" :style="input" /></td>
                </tr>
                <tr>
                  <td :style="labelCell">Work Order:</td>
                  <td :style="cell"><input v-model="report.workOrder" :style="input" /></td>
                </tr>
                <tr>
                  <td :style="labelCell">Part No. :</td>
                  <td :style="cell"><input v-model="report.partNo" :style="input" /></td>
                </tr>
                <tr>
                  <td :style="labelCell">Description:</td>
                  <td :style="cell"><input v-model="report.description" :style="input" /></td>
                </tr>
              </tbody>
            </table>
          </td>

          <!-- Right column: material spec / grade / P# across columns 1-3 -->
          <td style="width:50%;vertical-align:top;">
            <table style="border-collapse:collapse;width:100%;">
              <tbody>
                <tr>
                  <td :style="labelCell"></td>
                  <td :style="labelCell + 'text-align:center;'">1</td>
                  <td :style="labelCell + 'text-align:center;'">2</td>
                  <td :style="labelCell + 'text-align:center;'">3</td>
                </tr>
                <tr>
                  <td :style="labelCell + 'text-align:right;'">Material Spec:</td>
                  <td :style="cell"><input v-model="report.materialSpec1" :style="input" /></td>
                  <td :style="cell"><input v-model="report.materialSpec2" :style="input" /></td>
                  <td :style="cell"><input v-model="report.materialSpec3" :style="input" /></td>
                </tr>
                <tr>
                  <td :style="labelCell + 'text-align:right;'">Grade:</td>
                  <td :style="cell"><input v-model="report.grade1" :style="input" /></td>
                  <td :style="cell"><input v-model="report.grade2" :style="input" /></td>
                  <td :style="cell"><input v-model="report.grade3" :style="input" /></td>
                </tr>
                <tr>
                  <td :style="labelCell + 'text-align:right;'">P#:</td>
                  <td :style="cell"><input v-model="report.pNumber1" :style="input" /></td>
                  <td :style="cell"><input v-model="report.pNumber2" :style="input" /></td>
                  <td :style="cell"><input v-model="report.pNumber3" :style="input" /></td>
                </tr>
              </tbody>
            </table>
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <JointForm v-for="joint in report.joints.slice(0, 9)"
             :key="joint.jointNumber"
             :joint="joint" />
</template>

<script setup>
  import { storeToRefs } from 'pinia';
  import { useReportStore } from '@/store/reportStore';
  import { useLookupStore } from '@/store/lookupStore';
  import JointForm from '@/components/weld/JointForm.vue';

  const store = useReportStore();
  const { report } = storeToRefs(store);

  useLookupStore().load(true);

  const cell = 'border:1px solid #000;padding:2px 4px;font-size:12px;vertical-align:middle;';
  const labelCell = cell + 'font-weight:bold;background:#fff;white-space:nowrap;';
  const input =
        'width:100%;border:none;outline:none;font-size:12px;font-weight:bold;background:transparent;';
</script>

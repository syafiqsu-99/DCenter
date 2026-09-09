<script setup>
import { storeToRefs } from 'pinia';
import { useReportStore } from '@/store/reportStore';
import { useLookupStore } from '@/store/lookupStore';
import JointForm from '@/components/weld/JointForm.vue';

const store = useReportStore();
const { report } = storeToRefs(store);

useLookupStore().load();
</script>

<template>
  <v-card flat border class="mb-4">
    <v-card-title>Job {{ report.jobNumber }} — Header</v-card-title>
    <v-card-text>
      <v-row dense>
        <v-col cols="12" sm="3">
          <v-switch v-model="report.reportRequired" label="Report required" color="primary" inset />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="report.dateWelded" label="Date welded" type="date" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="report.workOrder" label="Work order" />
        </v-col>
        <v-col cols="12" sm="3">
          <v-text-field v-model="report.partNo" label="Part No." />
        </v-col>
        <v-col cols="12">
          <v-text-field v-model="report.description" label="Description" />
        </v-col>
      </v-row>

      <v-divider class="my-3" />

      <v-table density="compact">
        <thead>
          <tr>
            <th></th>
            <th>1</th>
            <th>2</th>
            <th>3</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Material Spec</td>
            <td><v-text-field v-model="report.materialSpec1" hide-details density="compact" /></td>
            <td><v-text-field v-model="report.materialSpec2" hide-details density="compact" /></td>
            <td><v-text-field v-model="report.materialSpec3" hide-details density="compact" /></td>
          </tr>
          <tr>
            <td>Grade</td>
            <td><v-text-field v-model="report.grade1" hide-details density="compact" /></td>
            <td><v-text-field v-model="report.grade2" hide-details density="compact" /></td>
            <td><v-text-field v-model="report.grade3" hide-details density="compact" /></td>
          </tr>
          <tr>
            <td>P#</td>
            <td><v-text-field v-model="report.pNumber1" hide-details density="compact" /></td>
            <td><v-text-field v-model="report.pNumber2" hide-details density="compact" /></td>
            <td><v-text-field v-model="report.pNumber3" hide-details density="compact" /></td>
          </tr>
        </tbody>
      </v-table>

      <v-row dense class="mt-2">
        <v-col cols="12" sm="6">
          <v-text-field v-model="report.engineerSupervisor" label="Engineer/Supervisor" />
        </v-col>
        <v-col cols="12" sm="6">
          <v-text-field v-model="report.qaInspector" label="QA Inspector" />
        </v-col>
      </v-row>
    </v-card-text>
  </v-card>

  <JointForm
    v-for="joint in report.joints"
    :key="joint.jointNumber"
    :joint="joint"
  />
</template>

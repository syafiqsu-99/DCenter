<template>
  <v-dialog :model-value="modelValue" max-width="440" @update:model-value="$emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center">
        <v-icon icon="mdi-alert-circle-outline" color="error" class="me-2" />
        {{ title }}
      </v-card-title>
      <v-card-text>
        <slot>
          This permanently removes <strong>{{ itemLabel }}</strong>. This cannot be undone.
        </slot>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="$emit('update:modelValue', false)">Cancel</v-btn>
        <v-btn color="error" variant="flat" :loading="loading" @click="$emit('confirm')">
          {{ confirmText }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup>
  defineProps({
    modelValue: Boolean,
    title: { type: String, default: 'Delete this item?' },
    itemLabel: { type: String, default: 'this item' },
    confirmText: { type: String, default: 'Delete' },
    loading: Boolean,
  });

  defineEmits(['update:modelValue', 'confirm']);
</script>

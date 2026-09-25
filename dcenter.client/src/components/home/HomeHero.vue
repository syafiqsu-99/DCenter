<template>
  <section class="hero">
    <div class="d-flex align-center ga-4 mb-4">
      <div class="hero__logo"><v-img :src="logo" width="48" height="48" alt="" /></div>
      <div class="text-overline hero__eyebrow">Emerson · Fisher weld shop</div>
    </div>
    <h1 class="hero__title">DCenter Operations Hub</h1>
    <p class="hero__subtitle">Weld shop job reports and welding consumables in one place.</p>

    <div class="d-flex flex-wrap align-center ga-3 mt-6">
      <template v-if="store.isSupervisor">
        <v-chip color="white" variant="outlined" prepend-icon="mdi-shield-account" size="large">
          Signed in as {{ store.supervisor.name }}
        </v-chip>
        <v-btn variant="outlined" color="white" prepend-icon="mdi-logout" @click="store.lockSupervisor()">Log out</v-btn>
      </template>
      <template v-else>
        <v-btn size="x-large" color="white" variant="flat" class="text-primary" prepend-icon="mdi-shield-lock-outline"
               @click="login">
          Supervisor login
        </v-btn>
        <span class="text-body-2 hero__note">Welders can open Report and Consumables without logging in.</span>
      </template>
    </div>
  </section>
</template>

<script setup>
  import { useRoute, useRouter } from 'vue-router'
  import { useConsumableStore } from '@/store/consumableStore'
  import logo from '@/assets/DCenter_No_bg.png'

  const store = useConsumableStore()
  const route = useRoute()
  const router = useRouter()

  function login() {
    router.replace({ path: route.path, query: { ...route.query, unlock: '1' } })
  }
</script>

<style scoped>
  .hero {
    max-width: 720px;
    animation: rise 0.4s ease both;
  }

  .hero__logo {
    background: #fff;
    border-radius: 14px;
    padding: 8px;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.25);
  }

  .hero__eyebrow {
    letter-spacing: 0.12em !important;
    opacity: 0.85;
  }

  .hero__title {
    font-size: clamp(2rem, 4.5vw, 3.25rem);
    font-weight: 700;
    line-height: 1.1;
    margin: 0;
  }

  .hero__subtitle {
    font-size: 1.15rem;
    opacity: 0.9;
    margin: 12px 0 0;
  }

  .hero__note {
    opacity: 0.8;
  }

  @keyframes rise {
    from { opacity: 0; transform: translateY(10px); }
    to { opacity: 1; transform: none; }
  }

  @media (prefers-reduced-motion: reduce) {
    .hero { animation: none; }
  }
</style>

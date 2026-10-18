import { createApp } from 'vue';
import { createPinia } from 'pinia';
import router from './router';
import './style.css';
import App from './App.vue';
import { useAuthStore } from './stores/auth';
import { initTheme } from './composables/useTheme';
import { i18n, rememberedLanguage, setLanguage } from './i18n';

initTheme();

const app = createApp(App);

const pinia = createPinia();
app.use(pinia);
app.use(router);
app.use(i18n);

// Start in the language this browser used last (English the first time); the user's preference, read
// from the server once signed in (MainLayout), then applies. Authentication is initialized before mounting.
const authStore = useAuthStore();
Promise.all([setLanguage(rememberedLanguage()).catch(() => setLanguage(null)), authStore.initialize()]).then(() => {
  app.mount('#app');
});


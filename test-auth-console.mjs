import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  
  // Track all console messages - especially auth-related
  page.on('console', msg => {
    const text = msg.text();
    console.log('CONSOLE:', text);
  });
  
  console.log('Loading https://DOMAIN_PLACEHOLDER/nInvoices/...\n');
  
  await page.goto('https://DOMAIN_PLACEHOLDER/nInvoices/', { waitUntil: 'domcontentloaded', timeout: 30000 });
  
  // Wait a bit for auth logic to run
  await page.waitForTimeout(5000);
  
  const finalUrl = page.url();
  console.log('\nFinal URL:', finalUrl);
  
  if (finalUrl.includes('/realms/') || finalUrl.includes('protocol/openid-connect')) {
    console.log('✓ Redirected to Keycloak login');
  } else {
    console.log('✗ No redirect - authentication may not be configured correctly');
  }
  
  await browser.close();
})();

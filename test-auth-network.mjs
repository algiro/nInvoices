import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  
  // Track all network requests
  page.on('request', request => {
    if (request.url().includes('api') || request.url().includes('auth')) {
      console.log('REQUEST:', request.method(), request.url());
    }
  });
  
  page.on('response', response => {
    if (response.url().includes('api') || response.url().includes('auth') || response.status() >= 400) {
      console.log('RESPONSE:', response.status(), response.url());
    }
  });
  
  // Listen for console messages
  page.on('console', msg => {
    const text = msg.text();
    if (text.includes('Auth') || text.includes('auth') || text.includes('Error') || text.includes('error')) {
      console.log('CONSOLE:', text);
    }
  });
  
  console.log('Loading page...');
  await page.goto('https://DOMAIN_PLACEHOLDER/nInvoices/', { waitUntil: 'networkidle', timeout: 30000 });
  
  await page.waitForTimeout(3000);
  console.log('Final URL:', page.url());
  
  await browser.close();
})();

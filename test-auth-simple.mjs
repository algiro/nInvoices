import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  
  // Track all console messages
  page.on('console', msg => console.log('BROWSER:', msg.text()));
  
  // Track navigation
  page.on('framenavigated', frame => {
    if (frame === page.mainFrame()) {
      console.log('NAVIGATED TO:', frame.url());
    }
  });
  
  console.log('Loading page...');
  
  try {
    const response = await page.goto('https://DOMAIN_PLACEHOLDER/nInvoices/', { 
      waitUntil: 'domcontentloaded',
      timeout: 30000 
    });
    
    console.log('Initial response status:', response?.status());
    
    // Wait for network idle or redirect
    await Promise.race([
      page.waitForURL(url => url.includes('keycloak') || url.includes('/realms/'), { timeout: 10000 }),
      page.waitForTimeout(10000)
    ]);
    
    const finalUrl = page.url();
    console.log('Final URL after 10s:', finalUrl);
    
    if (finalUrl.includes('keycloak') || finalUrl.includes('/realms/')) {
      console.log('✓ SUCCESS: Redirected to Keycloak!');
    } else {
      console.log('✗ FAIL: No redirect to Keycloak');
    }
    
  } catch (error) {
    console.log('Error:', error.message);
    console.log('Current URL:', page.url());
  }
  
  await browser.close();
})();

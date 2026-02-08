import { chromium } from 'playwright';

(async () => {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();
  
  // Listen for console messages
  page.on('console', msg => console.log('BROWSER:', msg.text()));
  
  // Listen for page errors
  page.on('pageerror', err => console.log('ERROR:', err.message));
  
  // Listen for navigation
  page.on('framenavigated', frame => {
    if (frame === page.mainFrame()) {
      console.log('NAVIGATED TO:', frame.url());
    }
  });
  
  console.log('Accessing https://DOMAIN_PLACEHOLDER/nInvoices/...');
  
  try {
    await page.goto('https://DOMAIN_PLACEHOLDER/nInvoices/', { 
      waitUntil: 'networkidle',
      timeout: 30000 
    });
    
    // Wait a bit for auth redirect
    await page.waitForTimeout(5000);
    
    const currentUrl = page.url();
    console.log('Final URL:', currentUrl);
    
    if (currentUrl.includes('keycloak') || currentUrl.includes('/realms/')) {
      console.log('✓ SUCCESS: Redirected to Keycloak login page!');
    } else {
      console.log('✗ FAIL: No redirect to Keycloak. Still on:', currentUrl);
      
      // Check if there are any auth-related errors in the page
      const pageContent = await page.content();
      console.log('Page title:', await page.title());
      
      // Get any visible error messages
      const errors = await page.$$eval('[class*="error"]', els => els.map(el => el.textContent));
      if (errors.length > 0) {
        console.log('Errors on page:', errors);
      }
    }
  } catch (error) {
    console.log('Navigation error:', error.message);
  }
  
  await browser.close();
})();

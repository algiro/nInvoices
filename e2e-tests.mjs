// Comprehensive E2E Test Suite for nInvoices
// Tests all major functionality across Chromium, Firefox, and Edge
import { chromium, firefox } from 'playwright';
import fs from 'fs';

const BROWSERS = ['chromium', 'firefox', 'msedge'];
const BASE_URL = 'http://localhost:3000';
const VIEWPORT = { width: 1920, height: 1080 };

const results = [];

// Utility to launch browser by name
async function launchBrowser(browserName) {
  const options = { headless: true };
  switch (browserName) {
    case 'chromium':
      return chromium.launch(options);
    case 'firefox':
      return firefox.launch(options);
    case 'msedge':
      return chromium.launch({ ...options, channel: 'msedge' });
    default:
      throw new Error(`Unknown browser: ${browserName}`);
  }
}

// Setup test context with error collection
async function setupTestContext(browserName) {
  const browser = await launchBrowser(browserName);
  const context = await browser.newContext({ viewport: VIEWPORT });
  const page = await context.newPage();
  const errors = [];
  const warnings = [];

  // Collect console messages
  page.on('console', msg => {
    const text = msg.text();
    if (msg.type() === 'error') {
      errors.push(`[Console Error] ${text}`);
    }
    if (msg.type() === 'warning' || text.includes('[Vue warn]')) {
      warnings.push(`[Warning] ${text}`);
    }
  });

  // Collect page errors
  page.on('pageerror', error => {
    errors.push(`[Page Error] ${error.message}`);
  });

  return { browser, context, page, browserName, errors, warnings };
}

// Cleanup test context
async function cleanupTestContext(ctx) {
  await ctx.browser.close();
}

// Take screenshot with browser name prefix
async function takeScreenshot(ctx, name) {
  const filename = `screenshots/${ctx.browserName}-${name}.png`;
  await ctx.page.screenshot({ path: filename, fullPage: true });
  return filename;
}

// Wait for navigation and network idle
async function waitForLoad(page) {
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(500);
}

// ============================================
// TEST FUNCTIONS
// ============================================

async function testDashboard(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    await page.goto(BASE_URL, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    screenshots.push(await takeScreenshot(ctx, 'dashboard'));
    
    // Check main elements are visible
    const dashboardTitle = page.locator('h1, h2').filter({ hasText: /dashboard|overview/i }).first();
    if (!await dashboardTitle.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Dashboard title not visible');
    }
    
    // Check navigation is visible
    const nav = page.locator('nav, .sidebar, .nav-menu').first();
    if (!await nav.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Navigation not visible');
    }
    
    // Check for customers link
    const customersLink = page.getByText('Customers').first();
    if (!await customersLink.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Customers link not visible');
    }
    
    return {
      browser: browserName,
      test: 'Dashboard',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Dashboard',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testCustomersList(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    await page.goto(`${BASE_URL}/customers`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    screenshots.push(await takeScreenshot(ctx, 'customers-list'));
    
    // Check page title
    const title = page.locator('h1, h2').filter({ hasText: /customer/i }).first();
    if (!await title.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Customers page title not visible');
    }
    
    // Check for "Add Customer" or "New Customer" button
    const addBtn = page.locator('button, a').filter({ hasText: /add|new|create/i }).first();
    if (!await addBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Add Customer button not visible');
    }
    
    return {
      browser: browserName,
      test: 'Customers List',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Customers List',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testCreateCustomer(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  const testCustomerName = `Test Customer ${browserName} ${Date.now()}`;
  
  try {
    await page.goto(`${BASE_URL}/customers/new`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    screenshots.push(await takeScreenshot(ctx, 'customer-form-empty'));
    
    // Check form is visible
    const form = page.locator('form').first();
    if (!await form.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Customer form not visible');
      return { browser: browserName, test: 'Create Customer', passed: false, errors: [...errors], warnings: [...warnings], screenshots };
    }
    
    // Fill in the form
    await page.locator('input[name="name"], #name, input[placeholder*="name" i]').first().fill(testCustomerName);
    await page.locator('input[name="fiscalId"], #fiscalId, input[placeholder*="fiscal" i], input[placeholder*="tax" i], input[placeholder*="vat" i]').first().fill('TEST123456');
    
    // Address fields
    const streetInput = page.locator('input[name="street"], #street, input[placeholder*="street" i]').first();
    if (await streetInput.isVisible()) {
      await streetInput.fill('123 Test Street');
    }
    
    const cityInput = page.locator('input[name="city"], #city, input[placeholder*="city" i]').first();
    if (await cityInput.isVisible()) {
      await cityInput.fill('Test City');
    }
    
    const zipInput = page.locator('input[name="zipCode"], input[name="postalCode"], #zipCode, #postalCode, input[placeholder*="zip" i], input[placeholder*="postal" i]').first();
    if (await zipInput.isVisible()) {
      await zipInput.fill('12345');
    }
    
    const countryInput = page.locator('input[name="country"], #country, input[placeholder*="country" i]').first();
    if (await countryInput.isVisible()) {
      await countryInput.fill('Test Country');
    }
    
    screenshots.push(await takeScreenshot(ctx, 'customer-form-filled'));
    
    // Submit the form
    const submitBtn = page.locator('button[type="submit"], button').filter({ hasText: /save|create|submit/i }).first();
    await submitBtn.click();
    await waitForLoad(page);
    
    screenshots.push(await takeScreenshot(ctx, 'customer-form-submitted'));
    
    // Check we're redirected or see success
    await page.waitForTimeout(2000);
    const currentUrl = page.url();
    if (currentUrl.includes('/new')) {
      // Might still be on form page - check for errors
      const errorMsg = page.locator('.error, .alert-danger, [class*="error"]').first();
      if (await errorMsg.isVisible({ timeout: 1000 }).catch(() => false)) {
        const errorText = await errorMsg.textContent();
        errors.push(`[Functional] Form submission error: ${errorText}`);
      }
    }
    
    return {
      browser: browserName,
      test: 'Create Customer',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    screenshots.push(await takeScreenshot(ctx, 'customer-form-error'));
    return {
      browser: browserName,
      test: 'Create Customer',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testCustomerDetailsAndTabs(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    // First go to customers list
    await page.goto(`${BASE_URL}/customers`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    
    // Click on first customer (if exists)
    const customerCard = page.locator('.customer-card, [data-customer], tr, .list-item').first();
    if (!await customerCard.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] No customers found in list');
      return { browser: browserName, test: 'Customer Details & Tabs', passed: false, errors: [...errors], warnings: [...warnings], screenshots };
    }
    
    await customerCard.click();
    await waitForLoad(page);
    screenshots.push(await takeScreenshot(ctx, 'customer-details'));
    
    // Check tabs are visible
    const tabs = ['Rates', 'Taxes', 'Templates'];
    for (const tabName of tabs) {
      const tab = page.getByText(tabName, { exact: false }).first();
      if (!await tab.isVisible({ timeout: 3000 }).catch(() => false)) {
        errors.push(`[Functional] Tab "${tabName}" not visible`);
      }
    }
    
    // Test clicking each tab
    for (const tabName of tabs) {
      const tab = page.getByText(tabName, { exact: false }).first();
      if (await tab.isVisible({ timeout: 1000 }).catch(() => false)) {
        await tab.click();
        await page.waitForTimeout(1000);
        screenshots.push(await takeScreenshot(ctx, `customer-tab-${tabName.toLowerCase()}`));
        
        // Check for Add button in each tab
        const addBtn = page.locator('button').filter({ hasText: /add/i }).first();
        if (!await addBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
          warnings.push(`[Visual] Add button not immediately visible in ${tabName} tab`);
        }
      }
    }
    
    return {
      browser: browserName,
      test: 'Customer Details & Tabs',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Customer Details & Tabs',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testAddTemplate(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    // Navigate to customer -> templates
    await page.goto(`${BASE_URL}/customers`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    
    // Click first customer
    const customerCard = page.locator('.customer-card, [data-customer], tr, .list-item').first();
    if (!await customerCard.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] No customers to test templates with');
      return { browser: browserName, test: 'Add Template', passed: false, errors: [...errors], warnings: [...warnings], screenshots };
    }
    
    await customerCard.click();
    await waitForLoad(page);
    
    // Click Templates tab
    const templatesTab = page.getByText('Templates').first();
    if (await templatesTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await templatesTab.click();
      await page.waitForTimeout(1000);
    }
    
    screenshots.push(await takeScreenshot(ctx, 'templates-tab'));
    
    // Click Add Template button
    const addTemplateBtn = page.locator('button').filter({ hasText: /add template/i }).first();
    if (!await addTemplateBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      // Try alternative selectors
      const altBtn = page.locator('button').filter({ hasText: /add|create|new/i }).first();
      if (await altBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await altBtn.click();
      } else {
        errors.push('[Functional] Add Template button not found');
        return { browser: browserName, test: 'Add Template', passed: false, errors: [...errors], warnings: [...warnings], screenshots };
      }
    } else {
      await addTemplateBtn.click();
    }
    
    await page.waitForTimeout(2000);
    screenshots.push(await takeScreenshot(ctx, 'template-modal-opened'));
    
    // Check modal opened
    const modal = page.locator('.modal-content, .modal, [role="dialog"]').first();
    if (!await modal.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Template modal did not open');
      return { browser: browserName, test: 'Add Template', passed: false, errors: [...errors], warnings: [...warnings], screenshots };
    }
    
    // Check form elements are visible
    const invoiceTypeSelect = page.locator('select, [role="combobox"]').first();
    if (!await invoiceTypeSelect.isVisible({ timeout: 3000 }).catch(() => false)) {
      errors.push('[Functional] Invoice type selector not visible');
    }
    
    const contentTextarea = page.locator('textarea').first();
    if (!await contentTextarea.isVisible({ timeout: 3000 }).catch(() => false)) {
      errors.push('[Functional] Template content textarea not visible');
    }
    
    // Check syntax guide button
    const syntaxBtn = page.locator('button').filter({ hasText: /syntax|guide/i }).first();
    if (await syntaxBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await syntaxBtn.click();
      await page.waitForTimeout(500);
      screenshots.push(await takeScreenshot(ctx, 'template-syntax-guide'));
    }
    
    // Check Load Sample button
    const loadSampleBtn = page.locator('button').filter({ hasText: /load sample/i }).first();
    if (await loadSampleBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      // Dismiss confirm dialog automatically
      page.on('dialog', dialog => dialog.accept());
      await loadSampleBtn.click();
      await page.waitForTimeout(1000);
      screenshots.push(await takeScreenshot(ctx, 'template-sample-loaded'));
    }
    
    // Close modal
    const closeBtn = page.locator('button').filter({ hasText: /cancel|close/i }).first();
    if (await closeBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await closeBtn.click();
      await page.waitForTimeout(500);
    }
    
    return {
      browser: browserName,
      test: 'Add Template',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    screenshots.push(await takeScreenshot(ctx, 'template-error'));
    return {
      browser: browserName,
      test: 'Add Template',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testAddRate(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    // Navigate to customer -> rates
    await page.goto(`${BASE_URL}/customers`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    
    const customerCard = page.locator('.customer-card, [data-customer], tr, .list-item').first();
    if (!await customerCard.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] No customers to test rates with');
      return { browser: browserName, test: 'Add Rate', passed: false, errors: [...errors], warnings: [...warnings], screenshots };
    }
    
    await customerCard.click();
    await waitForLoad(page);
    
    // Click Rates tab
    const ratesTab = page.getByText('Rates').first();
    if (await ratesTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await ratesTab.click();
      await page.waitForTimeout(1000);
    }
    
    screenshots.push(await takeScreenshot(ctx, 'rates-tab'));
    
    // Click Add Rate button
    const addRateBtn = page.locator('button').filter({ hasText: /add rate/i }).first();
    if (await addRateBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addRateBtn.click();
      await page.waitForTimeout(1000);
      screenshots.push(await takeScreenshot(ctx, 'rate-modal'));
      
      // Check modal content
      const modal = page.locator('.modal-content, .modal, [role="dialog"]').first();
      if (!await modal.isVisible({ timeout: 3000 }).catch(() => false)) {
        errors.push('[Functional] Rate modal did not open');
      }
      
      // Close modal
      const closeBtn = page.locator('button').filter({ hasText: /cancel|close/i }).first();
      if (await closeBtn.isVisible()) {
        await closeBtn.click();
      }
    } else {
      warnings.push('[Functional] Add Rate button not visible - may need to scroll');
    }
    
    return {
      browser: browserName,
      test: 'Add Rate',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Add Rate',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testAddTax(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    // Navigate to customer -> taxes
    await page.goto(`${BASE_URL}/customers`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    
    const customerCard = page.locator('.customer-card, [data-customer], tr, .list-item').first();
    if (!await customerCard.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] No customers to test taxes with');
      return { browser: browserName, test: 'Add Tax', passed: false, errors: [...errors], warnings: [...warnings], screenshots };
    }
    
    await customerCard.click();
    await waitForLoad(page);
    
    // Click Taxes tab
    const taxesTab = page.getByText('Taxes').first();
    if (await taxesTab.isVisible({ timeout: 3000 }).catch(() => false)) {
      await taxesTab.click();
      await page.waitForTimeout(1000);
    }
    
    screenshots.push(await takeScreenshot(ctx, 'taxes-tab'));
    
    // Click Add Tax button
    const addTaxBtn = page.locator('button').filter({ hasText: /add tax/i }).first();
    if (await addTaxBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await addTaxBtn.click();
      await page.waitForTimeout(1000);
      screenshots.push(await takeScreenshot(ctx, 'tax-modal'));
      
      // Check modal content
      const modal = page.locator('.modal-content, .modal, [role="dialog"]').first();
      if (!await modal.isVisible({ timeout: 3000 }).catch(() => false)) {
        errors.push('[Functional] Tax modal did not open');
      }
      
      // Close modal
      const closeBtn = page.locator('button').filter({ hasText: /cancel|close/i }).first();
      if (await closeBtn.isVisible()) {
        await closeBtn.click();
      }
    } else {
      warnings.push('[Functional] Add Tax button not visible');
    }
    
    return {
      browser: browserName,
      test: 'Add Tax',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Add Tax',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testInvoicesList(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    await page.goto(`${BASE_URL}/invoices`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    screenshots.push(await takeScreenshot(ctx, 'invoices-list'));
    
    // Check page title
    const title = page.locator('h1, h2').filter({ hasText: /invoice/i }).first();
    if (!await title.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Invoices page title not visible');
    }
    
    // Check for "Generate Invoice" or "New Invoice" button
    const generateBtn = page.locator('button, a').filter({ hasText: /generate|new|create/i }).first();
    if (!await generateBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Generate Invoice button not visible');
    }
    
    return {
      browser: browserName,
      test: 'Invoices List',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Invoices List',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testInvoiceGenerate(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    await page.goto(`${BASE_URL}/invoices/new`, { waitUntil: 'networkidle' });
    await waitForLoad(page);
    screenshots.push(await takeScreenshot(ctx, 'invoice-generate'));
    
    // Check form elements
    const customerSelect = page.locator('select, [role="combobox"]').first();
    if (!await customerSelect.isVisible({ timeout: 5000 }).catch(() => false)) {
      errors.push('[Functional] Customer selector not visible');
    }
    
    // Check for invoice type selector
    const typeLabels = ['Monthly', 'One-Time', 'Quarterly', 'Annual'];
    let typeFound = false;
    for (const label of typeLabels) {
      const typeOption = page.getByText(label, { exact: false }).first();
      if (await typeOption.isVisible({ timeout: 1000 }).catch(() => false)) {
        typeFound = true;
        break;
      }
    }
    if (!typeFound) {
      warnings.push('[Visual] Invoice type options not visible');
    }
    
    // Check for date picker
    const dateInput = page.locator('input[type="date"], input[placeholder*="date" i]').first();
    if (!await dateInput.isVisible({ timeout: 3000 }).catch(() => false)) {
      warnings.push('[Visual] Date input not visible');
    }
    
    return {
      browser: browserName,
      test: 'Invoice Generate',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Invoice Generate',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

async function testVisualConsistency(ctx) {
  const { page, browserName, errors, warnings } = ctx;
  const screenshots = [];
  
  try {
    // Test various pages for visual issues
    const pages = [
      { url: '/', name: 'dashboard' },
      { url: '/customers', name: 'customers' },
      { url: '/invoices', name: 'invoices' }
    ];
    
    for (const p of pages) {
      await page.goto(`${BASE_URL}${p.url}`, { waitUntil: 'networkidle' });
      await waitForLoad(page);
      
      // Check for invisible text (white on white, etc.)
      const invisibleText = await page.evaluate(() => {
        const issues = [];
        const elements = document.querySelectorAll('*');
        
        elements.forEach(el => {
          const style = window.getComputedStyle(el);
          const color = style.color;
          const bgColor = style.backgroundColor;
          
          // Check for very light text on very light background
          if (color && bgColor) {
            const parseRgb = (c) => {
              const match = c.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)/);
              return match ? [parseInt(match[1]), parseInt(match[2]), parseInt(match[3])] : null;
            };
            
            const textRgb = parseRgb(color);
            const bgRgb = parseRgb(bgColor);
            
            if (textRgb && bgRgb) {
              // Calculate luminance difference
              const textLum = (textRgb[0] * 0.299 + textRgb[1] * 0.587 + textRgb[2] * 0.114);
              const bgLum = (bgRgb[0] * 0.299 + bgRgb[1] * 0.587 + bgRgb[2] * 0.114);
              
              // If both are very light (>240) or both very dark (<15)
              if ((textLum > 240 && bgLum > 240) || (textLum < 15 && bgLum < 15)) {
                const text = el.innerText?.trim().substring(0, 50);
                if (text && text.length > 0) {
                  issues.push(`Low contrast text: "${text}" (text: ${color}, bg: ${bgColor})`);
                }
              }
            }
          }
        });
        
        return issues.slice(0, 5); // Limit to first 5 issues
      });
      
      invisibleText.forEach(issue => warnings.push(`[Visual] ${p.name}: ${issue}`));
      screenshots.push(await takeScreenshot(ctx, `visual-${p.name}`));
    }
    
    return {
      browser: browserName,
      test: 'Visual Consistency',
      passed: errors.length === 0,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  } catch (e) {
    errors.push(`[Exception] ${e.message}`);
    return {
      browser: browserName,
      test: 'Visual Consistency',
      passed: false,
      errors: [...errors],
      warnings: [...warnings],
      screenshots
    };
  }
}

// ============================================
// MAIN TEST RUNNER
// ============================================

async function runAllTests() {
  console.log('='.repeat(60));
  console.log('nInvoices E2E Test Suite');
  console.log('Browsers: Chromium, Firefox, Edge');
  console.log('='.repeat(60));
  console.log('');
  
  // Create screenshots directory
  if (!fs.existsSync('screenshots')) {
    fs.mkdirSync('screenshots');
  }
  
  const tests = [
    testDashboard,
    testCustomersList,
    testCreateCustomer,
    testCustomerDetailsAndTabs,
    testAddTemplate,
    testAddRate,
    testAddTax,
    testInvoicesList,
    testInvoiceGenerate,
    testVisualConsistency
  ];
  
  for (const browserName of BROWSERS) {
    console.log(`\n${'─'.repeat(60)}`);
    console.log(`Browser: ${browserName.toUpperCase()}`);
    console.log('─'.repeat(60));
    
    let ctx = null;
    
    try {
      ctx = await setupTestContext(browserName);
      
      for (const testFn of tests) {
        // Clear errors/warnings for each test
        ctx.errors = [];
        ctx.warnings = [];
        
        const result = await testFn(ctx);
        results.push(result);
        
        const status = result.passed ? '✅' : '❌';
        console.log(`${status} ${result.test}`);
        
        if (result.errors.length > 0) {
          result.errors.forEach(e => console.log(`   ERROR: ${e}`));
        }
        if (result.warnings.length > 0) {
          result.warnings.forEach(w => console.log(`   WARN: ${w}`));
        }
      }
    } catch (e) {
      console.log(`❌ Browser ${browserName} failed to launch: ${e.message}`);
    } finally {
      if (ctx) {
        await cleanupTestContext(ctx);
      }
    }
  }
  
  // Summary
  console.log('\n' + '='.repeat(60));
  console.log('TEST SUMMARY');
  console.log('='.repeat(60));
  
  const summary = {};
  
  for (const result of results) {
    if (!summary[result.browser]) {
      summary[result.browser] = { passed: 0, failed: 0, warnings: 0 };
    }
    if (result.passed) {
      summary[result.browser].passed++;
    } else {
      summary[result.browser].failed++;
    }
    summary[result.browser].warnings += result.warnings.length;
  }
  
  for (const [browser, stats] of Object.entries(summary)) {
    const total = stats.passed + stats.failed;
    const status = stats.failed === 0 ? '✅' : '❌';
    console.log(`${status} ${browser}: ${stats.passed}/${total} passed, ${stats.warnings} warnings`);
  }
  
  // Check if all passed
  const allPassed = Object.values(summary).every(s => s.failed === 0);
  console.log('\n' + (allPassed ? '✅ ALL TESTS PASSED' : '❌ SOME TESTS FAILED'));
  
  // Export detailed results
  fs.writeFileSync('test-results.json', JSON.stringify(results, null, 2));
  console.log('\nDetailed results saved to test-results.json');
  console.log('Screenshots saved to screenshots/');
  
  process.exit(allPassed ? 0 : 1);
}

runAllTests().catch(console.error);
